# Datamodel & Terminology

Forge 전역에서 사용하는 핵심 데이터 구조와 용어를 한 문서에 통합한다. 모든 설계/구현/테스트는 본 문서를 단일 소스로 삼아 명칭·필드 정의를 일관되게 유지해야 한다.

---

## 1. 공통 용어 (Terminology)

| 카테고리 | 용어 | 정의 / 설명 | 관련 문서 |
|----------|------|--------------|-----------|
| Frame | `FrameContext` | Session, Scenario, Timestamp, PersonStates, CameraPoses, Sensors를 포함한 프레임 메타데이터 | Architecture §3, Pipeline Spec §2 |
| Frame 결과 | `FrameGenerationResult` | `FrameContext` + `CaptureArtifacts`(카메라별 RawImageData) DTO | API Spec §3 |
| Capture 결과 | `RawImageData` | `camera_id`, `frame_id`, `width`, `height`, `pixel_format`, `pixels`(ArrayPool) | Pipeline Spec §2.1 |
| Camera Pose | `CameraPose` | `camera_id`, `position`, `rotation`, `timestamp` | Architecture §3, API Spec `/status`, Datamodel |
| Camera 메타 | `CameraMeta` | intrinsic/extrinsic/FOV/해상도/ID 메타. Config/Manifest/SceneMetadata 동일 명칭 | System Requirements, Manifest Schema |
| Camera 상태 | `CameraState` | Simulation 내부 런타임 상태(외부 문서에서는 Pose/Meta 사용) | Unity Integration Guide |
| 파이프라인 컨텍스트 | `FramePipelineContext` | Zero-copy 모드 Stage 간 공유 객체 | Pipeline Spec §2.0 |
| 세션 상태 | `SessionContext.currentFrame` | GenerationController가 마지막 발행한 frame_id | Checkpoint, Class Design |
| 파이프라인 상태 | `PipelineState.lastStoredFrame` | StorageWorker가 영구 저장 완료한 frame_id | Checkpoint, Class Design |
| Queue 비율 | `maxQueueRatio` | 워커 큐 길이/한도 최대값(0~1), `/status.queueDepthSummary`에서 동일 | Architecture §3.2.4, API `/status` |
| 백프레셔 | `BackPressureLevel` | `OK/CAUTION/SLOW/PAUSE`, 임계치: 0.7/0.9/1.0 | System Architecture, Pipeline Spec |
| 품질 모드 | `qualityMode` | `strict` or `relaxed`, Checkpoint/Manifest/`/status`/Config 동일 키 | System Requirements, API, Checkpoint |
| 체크포인트 버전 | `checkpointVersion` | `checkpoint-v1.x` 스키마 버전 공통 사용 | Checkpoint, Class Design |
| Metrics 필드 | `sensor_sync_offset_ms` | 센서-프레임 타이밍 편차 평균/99p | System Requirements, Robotics Extension |
| Occlusion Stage | `OcclusionWorker` | 파이프라인 Stage 명칭, Visibility 계산은 `VisibilityService` | Pipeline Spec, Architecture |

> 새로운 필드를 추가할 때는 본 파일을 먼저 업데이트하고, 참조 문서를 동시에 갱신한다.

---

## 2. 데이터 모델 (DataModel)

### 2.1 FrameContext (Phase 1+)

```json
{
  "sessionId": "string",
  "frameId": 12345,
  "timestamp": 123.456,
  "sceneName": "Factory",
  "scenarioId": "scenario_factory_day",
  "personStates": [
    {
      "globalPersonId": 1,
      "trackId": "cam01:15",
      "position": [0.5, 0.0, -3.2],
      "velocity": [0.1, 0.0, 0.0],
      "appearanceId": "outfit_a",
      "occlusion": 0.12,
      "visibility": 0.88
    }
  ],
  "cameraPoses": [
    {
      "cameraId": "cam01",
      "position": [0, 3, -5],
      "rotation": [0, 0, 0, 1],
      "timestamp": 123.456
    }
  ],
  "cameraMeta": [
    {
      "cameraId": "cam01",
      "intrinsic": [ ... 9 elements ... ],
      "extrinsic": [ ... 16 elements ... ],
      "resolution": {"width":1920,"height":1080},
      "fov": 60
    }
  ],
  "sensors": {
    "robotPose": {"position":[...], "rotation":[...], "timestamp":123.456},
    "lidar": {"sampleTimestamp":123.44, "ranges":[]}
  }
}
```
- Phase 1에서는 `sensors` 비활성 가능, Phase 4(로보틱스)에서 확장
- `cameraPoses`는 `CameraPose`, `cameraMeta`는 `CameraMeta` 리스트
- `personStates`는 Tracking/Annotation/Occlusion 단계 동일 구조

### 2.2 CameraPose

```json
{
  "cameraId": "cam01",
  "position": [x, y, z],
  "rotation": [x, y, z, w],
  "timestamp": 123.456
}
```
- 모든 문서/코드는 위 구조를 사용, 다른 이름은 내부 구현 전용

### 2.3 CameraMeta

```json
{
  "cameraId": "cam01",
  "intrinsic": [...],
  "extrinsic": [...],
  "resolution": {"width":1920,"height":1080},
  "fov": 60,
  "mode": "static|mobile"
}
```
- Manifest, Config, SceneMetadata에서 동일 구조

### 2.4 FramePipelineContext (Zero-copy, Phase 2+)

```csharp
class FramePipelineContext {
    FrameContext Frame { get; }
    RawImageData[] Images { get; set; }
    DetectionData Detection { get; set; }
    TrackingData Tracking { get; set; }
    LabeledFrame Label { get; set; }
    EncodedFrame Encoded { get; set; }
    Dictionary<string, StageStatus> StageStatuses { get; }
}
```
- Stage별 할당 필드는 단일 소스로 위 구조를 따른다.

---

본 파일 수정 시 관련 문서(Architecture, Pipeline Spec, API Spec 등)와 용어 정의를 함께 갱신한다.
