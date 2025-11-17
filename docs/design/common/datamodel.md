## 공통 데이터 모델 (DataModel)

본 문서는 Forge 전역에서 공유하는 핵심 DTO 구조를 정의한다. 모든 설계/구현/문서는 해당 정의를 단일 소스로 참조해야 한다.

---

### 1. FrameContext (Phase 1+)

```json
{
  "sessionId": "string",
  "frameId": 12345,
  "timestamp": 123.456,          // 시뮬레이션 월드 시간 (초)
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

- Phase 1에서는 `sensors` 필드가 비활성화될 수 있으며, Phase 4(로보틱스)에서 확장된다.
- `cameraPoses`는 `CameraPose` DTO 리스트, `cameraMeta`는 `CameraMeta` 리스트를 참조한다.
- `personStates`는 Tracking/Annotation/Occlusion 단계에서 동일 구조를 사용한다.

### 2. CameraPose

```json
{
  "cameraId": "cam01",
  "position": [x, y, z],
  "rotation": [x, y, z, w],
  "timestamp": 123.456
}
```

- 모든 문서/코드는 위 구조를 사용해 Pose 정보를 저장한다. 다른 이름(`CameraState`, `CameraTransform`)은 내부 구현에서만 사용한다.

### 3. CameraMeta

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

- Manifest, Config, SceneMetadata에서 동일 구조를 사용한다.

### 4. FramePipelineContext (Zero-copy, Phase 2+)

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

- Stage별로 할당되는 필드는 단일 소스로 위 구조를 따른다.

---

본 파일을 수정할 때는 `docs/design/common/terminology.md`와 관계 문서(Architecture, Pipeline Spec, API Spec 등)를 동시에 갱신한다.
