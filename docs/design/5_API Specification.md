Forge

---

## 1. 문서 목적 (Purpose)

본 문서는 Forge의 주요 API 명세를 정의한다.  
API 계약을 명확히 함으로써 레이어 간 결합도를 낮추고 기능별 병행 개발, 테스트 자동화, 운영 모니터링을 가능하게 한다.

---

## 2. API 개요 (API Overview)

| 구분                             | 설명                                                 | 소비자                            |
| ------------------------------ | -------------------------------------------------- | ------------------------------ |
| Orchestration ↔ Simulation API | C# Orchestration Layer가 Unity Simulation Layer를 제어 | SessionManager / Unity Adapter |
| Configuration API              | 사용자/파이프라인이 참조하는 `config.json` 명세                   | CLI, Web UI, 테스트 도구            |
| Output Data API                | 파이프라인이 내보내는 라벨/메타 데이터 구조                           | 학습 파이프라인, QA, 외부 소비자           |

---

## 3. Orchestration ↔ Simulation API

본 API는 **Simulation Gateway 모드**가 `remote` 또는 `distributed`일 때 적용된다.
기본 모드(`InProcessSimulationGateway`)에서는 동일 프로세스 내에서 직접 메서드 호출을 사용하므로 HTTP 트래픽이 발생하지 않는다.
Config에서 `simulation.gateway.mode=remote` 혹은 `distributed`를 설정하면 Orchestration Layer가 `HttpSimulationGateway`를 사용하여 본 명세에 맞춰 HTTP 클라이언트를 활성화한다.

### 3.1 통신 모델

- **프로토콜**: HTTP/1.1 (로컬 호스트 기반, 기본 포트 `8080`)
- **엔드포인트 베이스 URI**: `http://localhost:8080/api/simulation`
- **데이터 형식**: `application/json; charset=utf-8`
- **보안/인증 옵션 (NFR-12 대응)**:
  - 기본 모드: `127.0.0.1`에만 바인딩하며 인증 생략.
  - `remote/distributed` 모드: mTLS 혹은 API Key 필수. 기본 바인딩은 `127.0.0.1`; 필요 시 `allowedHosts`에 명시한 주소만 허용.
  - `/status`를 포함해 **모든 엔드포인트**에 동일 인증을 강제한다. 무인증 상태 조회 금지.
  - HTTP 헤더 `X-Api-Key`(단일 키) 또는 `Authorization: Bearer <token>` 사용 가능. CLI/Config에서 `simulation.gateway.auth.*`로 설정.
  - 보안 가이드(`docs/design/10_Security_and_Compliance.md`)의 “접근 제어/데이터 격리” 원칙을 준수하며, 운영 시 `/status` 노출 범위를 내부 대시보드로 한정한다.
  - **필수 구성**: `HttpAuthMiddleware`(Class Design 문서)와 `ConfigSanitizer`를 API 서버 파이프라인에 포함하여 인증/허용 호스트/민감정보 필터링을 일관되게 적용한다.
- **재시도 권장 정책**:
  - 네트워크 타임아웃/5xx 응답 시 지수 백오프(초기 1초, 최대 5회)로 재시도
  - 4xx는 클라이언트 오류로 간주하고 즉시 사용자에게 전파
- **버전 호환**:
  - 서버는 `X-Engine-Version` 헤더로 현재 엔진 버전 명시
  - 클라이언트는 `Accept-Version: v1` 형태로 원하는 API 버전 요청 가능
  - Major 버전이 다르면 426 Upgrade Required를 반환하고 `/status`에서 지원 버전 목록 노출
- **상태 머신**:

| 상태        | 설명               | 전이 트리거                                      |
| --------- | ---------------- | ------------------------------------------- |
| `idle`    | 세션 없음, 자원 유휴     | `/session/init`                             |
| `ready`   | 리소스 로드 완료, 대기 상태 | `/session/start`                            |
| `running` | 프레임 루프 진행 중      | `/session/stop`, `/session/pause` (Phase 2) |
| `paused`  | 프레임 루프 중단, 재개 가능 | `/session/resume`                           |
| `error`   | 치명 오류 발생         | `/session/stop` 후 `/session/init`           |

### 3.2 에러 모델

모든 엔드포인트는 실패 시 아래 공통 스키마를 반환한다.

```json
{
  "status": "error",
  "code": "SCENE_LOAD_FAILED",
  "message": "Failed to load scene: Office",
  "details": {
    "scene": "Office",
    "stackTrace": "..."
  }
}
```

- `code`: 고정 문자열 (`INVALID_REQUEST`, `SCENE_LOAD_FAILED`, `SIMULATION_CRASHED` 등)
- `details`: JSON 객체, 필드 자유.

### 3.3 엔드포인트 요약

| Method & Path             | 설명                         | 주요 파라미터                                     | 성공 응답                     | 비고                               |
| ------------------------- | -------------------------- | ------------------------------------------- | ------------------------- | ---------------------------------- |
| `POST /session/init`      | Scene Pool 및 리소스 초기화       | `sessionId`, `scenePool[]`, `engineVersion`, `authMode` | `{status:"success"}`      | Scene 캐시 구축 및 버전 호환성 검사 |
| `POST /session/start`     | 프레임 루프 시작                  | `SessionConfig` 전체                          | `{status:"success"}`      | 시작 전 `init` 필수, 인증 필요        |
| `POST /session/stop`      | 세션 중단/리소스 해제               | 없음                                          | `{status:"success"}`      | 비정상 종료 시에도 호출, 재시도 가능     |
| `POST /scenario/activate` | 런타임 Scene/환경 전환 (Phase 2+) | `sceneName`, `timeWeather`, `randomization` | `{status:"success"}`      | Scene 전환 완료 후 응답, 버전 체크 포함  |
| `GET /status`             | 실행 상태 확인                   | 쿼리 없음                                       | `{status:"running", ...}` | 모니터링/Progress UI, 인증 옵션 선택    |

#### `POST /session/init`

- **Request**
  ```json
  {
    "sessionId": "session_20231027_factory",
    "scenePool": ["Factory", "Office"],
    "engineVersion": "1.0.0",
    "authMode": "api-key"
  }
  ```
- **Response 200**
  ```json
  { "status": "success", "message": "Initialization complete. Ready for session start." }
  ```
- **Response 409** (세션 이미 존재)
  ```json
  { "status": "error", "code": "SESSION_ALREADY_INITIALIZED", "message": "Active session session_20231027_factory exists." }
  ```

#### `POST /session/start`

- **Request**: Configuration API에서 정의한 SessionConfig 전체
- **Response 202** (비동기 시작)
  ```json
  { "status": "accepted", "message": "Session start scheduled.", "sessionId": "session_20231027_factory" }
  ```
- **Response 400**: Config validation 실패 (예: 카메라 수 0)

#### `POST /session/stop`

- **Response 200**
  ```json
  { "status": "success", "message": "Session stopped.", "processedFrames": 45213 }
  ```
- **Response 410**: 세션 없음 (`SESSION_NOT_FOUND`)

#### `POST /scenario/activate` (Phase 2+)

- **Request**
  ```json
  {
    "sceneName": "Office",
    "timeWeather": { "timeOfDay": "night", "brightness": 0.2 },
    "randomization": { "noiseLevel": 0.1 }
  }
  ```
- **Response 200**
  ```json
  { "status": "success", "message": "Scenario 'Office' activated." }
  ```

#### `GET /status`

```json
{
  "status": "running",
  "engineVersion": "1.2.0",
  "currentFrame": 12345,
  "targetFrame": 100000,
  "fps": 14.5,
  "activeScene": "Factory",
  "queueDepthSummary": 0.42,
  "mobileCameras": [
    {"id": "bot_cam_01", "poseTimestamp": 12345, "position": [1.2, 0.5, -3.0], "rotationEuler": [0, 45, 0]}
  ],
  "supportedVersions": ["v1", "v1beta"],
  "authMode": "api-key"
}
```

보안/노출 최소화:
- `queueDepthSummary`는 워커별 상세를 숨기고 0~1 스칼라(최대 큐 사용률)만 제공한다.
- 내부 리소스 경로, 사용자명 등 민감 정보는 `/status` 응답에 포함하지 않는다.
- 인증이 없는 요청은 401로 거부하며, 로컬 바인딩 모드라도 프런트엔드/CLI 외 노출을 금지한다.
- 구현 체크리스트:
  - 인증 미들웨어가 `/status`에도 적용되는지 통합 테스트한다.
  - `allowedHosts` 설정이 적용되어 원격 접근이 제한되는지 확인한다.
  - 상태 응답이 내부 상세(큐별 길이, 경로) 대신 요약 지표만 반환하는지 계약 테스트로 고정한다.
  - 이동형 카메라가 존재할 경우 pose 요약(최대 위치/회전 변화량 등)만 제공하고, 세부 궤적은 인증된 manifest/로그에서 확인하도록 한다.

CLI/SDK 구성 시:
- `dotnet run -- --api-key <KEY>` 형식으로 API Key를 전달하거나,
- 환경 변수 `FORGE_API_KEY` / `FORGE_BEARER`를 설정하면 자동으로 `X-Api-Key` 또는 `Authorization` 헤더에 주입되도록 한다.
구체적인 설정 방법은 CLI 도움말(`--help`)과 동일하게 유지한다.

---

## 4. 설정 API (Configuration API)

-   **파일 형식**: JSON (`UTF-8`)
-   **검증 단계**: CLI에서 JSON Schema로 1차 검증 → Orchestration Layer에서 추가 비즈니스 룰 검증

### 4.1 최상위 필드

| 필드 | 타입 | 필수 | 설명 / 제약 | 예시 |
|------|------|------|-------------|------|
| `sessionId` | string | ✅ | 고유 세션 ID (`[a-z0-9_-]+`) | `"session_factory_run_001"` |
| `totalFrames` | integer | ✅ | 전체 생성 프레임 수 (`>=1`) | `100000` |
| `outputDirectory` | string | ✅ | 절대 경로 | `"/data/output"` |
| `scenes[]` | array | ✅ | Scene 시퀀스 정의 | 아래 참조 |
| `cameras[]` | array | ✅ | 1~6개, 고유 `id` 필수 | 아래 참조 |
| `crowd` | object | ✅ | 인원/행동 정의 | 아래 참조 |
| `timeWeather` | object | ✅ | 기본 시간/조명/날씨 | 아래 참조 |
| `simulationGateway` | object | ✅ | Unity와의 통신 모드/보안/포트 정의 | 아래 4.4 |
| `randomization` | object | Phase 2+ | Domain Randomization 파라미터 |  |
| `output` | object | ✅ | 이미지/라벨 포맷, manifest 옵션 |  |
| `pipeline` | object | Phase 2+ | 워커 병렬도, 큐 사이즈 |  |

### 4.2 Scene 섹션

```json
"scenes": [
  { "name": "Factory", "durationFrames": 60000 },
  { "name": "Office", "durationFrames": 40000 }
]
```

- `durationFrames` 합계 = `totalFrames`
- Phase 2부터 Scene 전환 시 `scenario/activate` 요청으로 반영

### 4.3 Camera 섹션

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | string | 고유 카메라 식별자 (`cam01`, `bot_cam_01` 등) |
| `type` | string | `static` 또는 `mobile`. 기본값 `static`. |
| `resolution` | string | `"WxH"` 형식. 내부에서 숫자 배열로 파싱 |
| `fov` | number | 수평 FOV (도) |
| `position` | float[3] | 초기 Unity world 좌표. mobile 타입일 경우 시작 포즈. |
| `rotation` | float[3] | 초기 Euler 각 |
| `sensorNoise` | object | (선택) 감마/노이즈/rolling shutter/motion blur 옵션 |
| `mobile` | object | `type="mobile"`일 때 필수. 아래 상세 |

**`mobile` 객체 스키마 (필수/선택 필드)**

| 필드 | 타입 | 설명 |
|------|------|------|
| `path.waypoints[]` | array | 각 waypoint는 `{ "position": [x,y,z], "waitSeconds": <float?> }` 형태. 최소 2개 필수. |
| `path.loop` | bool | 마지막 waypoint 이후 처음으로 반복 여부. 기본 `false`. |
| `path.maxSpeed` | number | m/s 단위 최대 이동 속도. |
| `path.maxAngularSpeed` | number | deg/s 단위 회전 한계. |
| `path.navmeshArea` | string? | (선택) 이동에 사용할 NavMesh 영역 이름. |
| `controller` | object | PID/보간 파라미터 (`positionGain`, `rotationGain` 등). 옵션이지만 기본값 제공. |
| `sensor.rollingShutter` | bool | rolling shutter 시뮬레이션 여부. |
| `sensor.motionBlur.exposureMs` | number | motion blur 노출 시간(ms). |
| `poseLogging.enabled` | bool | pose 기록 여부 (기본 true). |
| `poseLogging.sampleRate` | number | pose 샘플링 주기(Hz). 생략 시 1frame=1sample. |
| `poseLogging.output` | string | 사용자 정의 pose 파일 경로 (기본 `camera_poses/{cameraId}.csv`). |

### 4.4 Simulation Gateway 섹션

Config 예시:
```json
"simulationGateway": {
  "mode": "inprocess",
  "host": "127.0.0.1",
  "port": 8080,
  "auth": {
    "type": "api-key",
    "apiKeyEnv": "FORGE_API_KEY"
  },
  "allowedHosts": ["127.0.0.1"]
}
```

| 필드 | 필수 | 설명 |
|------|------|------|
| `mode` | ✅ | `inprocess`, `remote`, `distributed` 중 선택. Orchestration이 사용할 `ISimulationGateway` 구현 타입을 결정한다. |
| `host` / `port` | remote+ | HTTP 모드에서 바인딩할 주소. 기본 `127.0.0.1:8080`. |
| `auth.type` | remote+ | `none`, `api-key`, `mtls`. `mode=inprocess`일 때 자동으로 `none`. |
| `auth.apiKeyEnv` | api-key | CLI/서비스가 사용할 환경 변수명. 없으면 Config에 직접 키를 저장하지 않는 것이 원칙. |
| `auth.certPath`/`keyPath` | mtls | mTLS 구성에 필요한 인증서 경로. |
| `allowedHosts[]` | remote+ | HTTP 서버가 수락할 호스트 화이트리스트. 기본 `["127.0.0.1"]`. |

**적용 규칙 및 구현 클래스**
- `mode=inprocess`: Unity 프로세스 내부에서 `InProcessSimulationGateway` (MonoBehaviour) 사용. HTTP 설정 무시.
- `mode=remote`: Unity가 별도 프로세스. Orchestration은 `HttpSimulationGateway` 사용하여 `/session/*` REST API 호출. API Specification §3 전체 적용.
- `mode=distributed`: Master/Worker 통신은 §8 분산 아키텍처와 동일하며, Worker 측은 `HttpSimulationGateway`를 통해 `remote` 모드로 동작.
- NFR-12 준수를 위해 remote/distributed 모드에서는 반드시 `auth.type`과 `allowedHosts`를 명시한다.

### 4.4 Crowd & Behavior

```json
"crowd": {
  "minCount": 20,
  "maxCount": 30,
  "behavior": {
    "idleProbability": 0.1,
    "walkSpeedRange": [0.8, 1.5],
    "groupMove": { "enabled": true, "sizeRange": [3, 6] }
  }
}
```

- `minCount` ≤ `maxCount`
- Phase 3에서 이벤트 행동(넘어짐 등) 추가 예정

### 4.5 TimeWeather & Randomization

```json
"timeWeather": {
  "timeOfDay": "day",      // day | night | sunrise | sunset
  "brightness": 1.0,
  "weather": "clear"
},
"randomization": {
  "enabled": true,
  "brightnessRange": [0.8, 1.2],
  "colorTemperatureRange": [4000, 8000],
  "cameraNoiseLevel": 0.05,
  "weatherPool": ["clear", "rainy"]
}
```

### 4.6 Output 섹션

```json
"output": {
  "imageFormat": "jpg",
  "labelFormats": ["json", "yolo"],
  "manifest": { "enabled": true, "schemaVersion": "1.0.0" }
}
```

- `labelFormats`는 중복 불가, 최소 1개
- Phase 3: Edge-friendly (`tflite`, `onnx`) 추가 예정

### 4.7 Pipeline 섹션 (선택)

```json
"pipeline": {
  "workerConcurrency": {
    "capture": 1,
    "detection": 2,
    "encode": 2
  },
  "queueSize": {
    "capture": 512,
    "encode": 2048
  },
  "checkpoint": { "enabled": true, "intervalFrames": 1000 }
}
```

---

## 5. 출력 데이터 API (Output Data API)

-   **파일 형식**: JSON (카메라별 프레임 단위) / 추가 포맷(JSON Lines, COCO, YOLO 등은 별도 문서 참조)
-   **위치**: `output_root/session_xxx/labels/json/{camera_id}/{frame_id:06d}.json`

### 5.1 스키마 정의

| 필드 | 타입 | 설명 |
|------|------|------|
| `sessionId` | string | 실행 세션 ID |
| `frameId` | integer | 0부터 시작하는 글로벌 프레임 번호 |
| `timestamp` | string | ISO 8601 |
| `sceneName` | string | FrameCaptured 시점의 Scene |
| `camera` | object | camera metadata |
| `annotations[]` | array | 감지된 사람/객체 리스트 |

**camera 객체**

| 필드 | 설명 |
|------|------|
| `id` | 카메라 ID |
| `resolution` | `[width, height]` |
| `intrinsics` | 3x3 행렬 |
| `extrinsics.position` | [x,y,z] |
| `extrinsics.rotation` | [pitch,yaw,roll] |

**annotation 객체**

| 필드 | 타입 | 설명 |
|------|------|------|
| `globalPersonId` | integer | 세션 전역 ID |
| `trackId` | integer | 카메라별 Track ID |
| `bbox2d` | object | 픽셀 단위 bbox (`xmin`, `ymin`, `xmax`, `ymax`) |
| `confidence` | float | GT이므로 기본 1.0 (재학습시 가중치로 사용 가능) |
| `occlusion` | float | 0~1, occluded 비율 (Phase 2+) |
| `visibility` | float | 0~1, 노출 비율 (Phase 2+) |
| `attributes` | object | (선택) appearance tags, 행동 태그 등 |

### 5.2 예시 (`/labels/json/cam01/000000.json`)

```json
{
  "sessionId": "session_factory_run_001",
  "frameId": 0,
  "timestamp": "2023-10-27T10:00:00.000Z",
  "sceneName": "Factory",
  "camera": {
    "id": "cam01",
    "resolution": [1920, 1080],
    "intrinsics": [[1050.0, 0, 960.0], [0, 1050.0, 540.0], [0, 0, 1]],
    "extrinsics": {
      "position": [10, 5, -20],
      "rotation": [15, 180, 0]
    }
  },
  "annotations": [
    {
      "globalPersonId": 101,
      "trackId": 1,
      "bbox2d": { "xmin": 540, "ymin": 320, "xmax": 680, "ymax": 710 },
      "confidence": 1.0,
      "occlusion": 0.15,
      "visibility": 0.95,
      "attributes": { "behavior": "walk", "wardrobe": "worker_blue" }
    }
  ]
}
```

### 5.3 파생 포맷

- **YOLO**: 카메라별 `{frame}.txt`, `class x_center y_center width height visibility`
- **COCO**: 세션 단위 `coco_annotations.json`
- **ReID Dataset** (Phase 2+): person_id 기반 디렉토리 구조
  ```
  /output/reid_dataset/
    person_0001/
      cam01_frame_000123.jpg
      cam02_frame_000456.jpg
    metadata.json
  ```
- **Manifest/Stats**: `meta/` 폴더에서 SessionConfig 요약, validation 결과 제공
