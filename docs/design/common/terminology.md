## 공통 용어집 (Terminology)

Forge 문서 전반에서 사용하는 주요 개념/필드/신호 이름을 아래 표에 통일한다.  
모든 설계/요구사항/스키마 문서는 본 용어집을 참고하여 동일 명칭을 사용해야 한다.

| 카테고리 | 용어 | 정의 / 설명 | 관련 문서 |
|----------|------|--------------|-----------|
| Frame | `FrameContext` | Session, Scenario, Timestamp, PersonStates, CameraPoses, Sensors를 포함한 프레임 메타데이터. DataModel 파일에 단일 구조 정의 | Architecture §3, Pipeline Spec §2, DataModel.md |
| Frame 결과 | `FrameGenerationResult` | `FrameContext` + `CaptureArtifacts`(카메라별 RawImageData)의 묶음. HTTP Gateway/Remote 모드에서 반환되는 DTO 명칭 | API Spec §3 |
| Capture 결과 | `RawImageData` | `camera_id`, `frame_id`, `width`, `height`, `pixel_format`, `pixels`(ArrayPool) | Pipeline Spec §2.1 |
| Camera Pose | `CameraPose` | `camera_id`, `position`, `rotation`, `timestamp` 필드를 갖는 Pose DTO. FrameContext 및 Manifest에 동일한 형식으로 포함 | Architecture §3, API Spec `/status`, DataModel.md |
| Camera 메타 | `CameraMeta` | 카메라 intrinsic/extrinsic/FOV/해상도/ID를 묶는 메타 정보. Config/Manifest/SceneMetadata에서 동일 명칭 사용 | System Requirements FR-06, Manifest Schema |
| Camera 상태 | `CameraState` | (필요 시) Simulation 내부에서만 사용하는 카메라 런타임 상태. 외부 문서에서는 CameraPose/CameraMeta 용어를 사용 | Unity Integration Guide |
| 파이프라인 컨텍스트 | `FramePipelineContext` | Zero-copy 모드에서 Stage 간 공유되는 단일 객체. Stage별 필드는 “Mutable Fields” 표를 따름 | Pipeline Spec §2.0 |
| 세션 상태 | `SessionContext.currentFrame` | GenerationController가 마지막으로 발행한 frame_id. Checkpoint/Progress/Manifest에서 동일 필드명 사용 | System Requirements FR-21, Class Design 3.1 |
| 파이프라인 상태 | `PipelineState.lastStoredFrame` | StorageWorker가 영구 저장 완료한 frame_id. Checkpoint/Manifest/Progress 요약에서 일관된 명칭 사용 | Checkpoint Doc §3, Class Design §4.1 |
| Queue 비율 | `maxQueueRatio` | PipelineCoordinator가 워커 큐 길이 / 큐 한도를 계산한 최대값(0~1). `/status.queueDepthSummary`는 `maxQueueRatio` 값 그대로 노출 | Architecture §3.2.4, API Spec `/status` |
| 백프레셔 | `BackPressureLevel` | `OK`, `CAUTION`, `SLOW`, `PAUSE`. `CAUTION`: maxQueueRatio 0.7~0.9, `SLOW`: 0.9~1.0, `PAUSE`: 1.0 이상. 모든 문서 동일 임계치 사용 | System Architecture §3.2.4-B, Pipeline Spec §2.3 |
| 품질 모드 | `qualityMode` | `strict` 또는 `relaxed`. Checkpoint, Manifest, `/status`, Config 모두 동일 키 사용 | System Requirements FR-29, API Spec, Checkpoint Doc |
| 체크포인트 버전 | `checkpointVersion` | `checkpoint-v1.x` 스키마 버전. Checkpoint 문서/SessionManager/Resume 흐름/Manifest reference가 동일 값을 사용 | Checkpoint Doc §2, Class Design §4.1 |
| Metrics 필드 | `sensor_sync_offset_ms` | Robotics 세션 시 센서-프레임 타이밍 편차 평균/99p. Manifest.performanceSummary와 `/metrics`에서 동일 명칭 | System Requirements FR-42, Robotics Extension §3 |
| Occlusion Stage | `OcclusionWorker` | 파이프라인 Stage 명칭은 OcclusionWorker로 통일. Visibility 계산 서비스는 `VisibilityService`로 명시 | Pipeline Spec §4.6, Architecture §3.3 |

> ⚠️ 새로운 필드를 추가할 때는 본 파일을 먼저 업데이트하고, 참조 문서를 동시에 갱신해야 한다.

**FrameContext / CameraMeta / Pose 등 데이터 구조는 `docs/design/common/datamodel.md`를 단일 소스로 사용한다.**
