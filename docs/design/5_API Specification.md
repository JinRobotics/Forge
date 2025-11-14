CCTV Synthetic Data Generation Engine

---

## 1. 문서 목적 (Purpose)

본 문서는 CCTV Synthetic Data Generation Engine의 주요 API 명세를 정의한다.  
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

C# 백엔드(클라이언트)가 Unity 시뮬레이터(서버)를 제어하는 모델을 기준으로 한다.

### 3.1 통신 모델

- **프로토콜**: HTTP/1.1 (로컬 호스트 기반, 기본 포트 `8080`)
- **엔드포인트 베이스 URI**: `http://localhost:8080/api/simulation`
- **데이터 형식**: `application/json; charset=utf-8`
- **보안/인증 옵션**:
  - 기본: 로컬 프로세스 간 통신이라 인증 생략
  - 분산/원격 배포 시 mTLS 또는 IPC 채널 적용
  - HTTP 헤더 `X-Api-Key`(단일 키) 또는 `Authorization: Bearer <token>` 사용 가능하도록 서버 설정 옵션 제공 (필요 시 CLI에서 전달)
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
| `POST /session/init`      | Scene Pool 및 리소스 초기화       | `sessionId`, `scenePool[]`, `engineVersion` | `{status:"success"}`      | Scene 캐시 구축 및 버전 호환성 검사 |
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
    "engineVersion": "1.0.0"
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
  "queueDepths": {
    "capture": 12,
    "encode": 420
  },
  "supportedVersions": ["v1", "v1beta"],
  "authMode": "api-key"
}
```

CLI/SDK 구성 시:
- `dotnet run -- --api-key <KEY>` 형식으로 API Key를 전달하거나,
- 환경 변수 `CCTV_SIM_API_KEY` / `CCTV_SIM_BEARER`를 설정하면 자동으로 `X-Api-Key` 또는 `Authorization` 헤더에 주입되도록 한다.
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
| `id` | string | 고유 카메라 식별자 (`cam01` 등) |
| `resolution` | string | `"WxH"` 형식. 내부에서 숫자 배열로 파싱 |
| `fov` | number | 수평 FOV (도) |
| `position` | float[3] | Unity world 좌표 |
| `rotation` | float[3] | Euler 각 |
| `sensorNoise` | object | (선택) 감마/노이즈 옵션 |

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
