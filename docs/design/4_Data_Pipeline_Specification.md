
## 1. 목적 (Purpose)

> 모든 용어/데이터 구조는 `docs/design/common/terminology.md`와 `docs/design/common/datamodel.md`를 따른다.

본 문서는 Forge의 **데이터 파이프라인**을  
구체적으로 정의한다.

- Stage(단계)별 입력/출력/큐/스레드/오류/백프레셔(back-pressure)를 명시한다.
- ID 정책(frame_id, camera_id, global_person_id 등)을 명시한다.
- 디렉터리/파일명/manifest 구조를 정의한다.
- 성능/안정성/운영 관점에서 구현 시 반드시 지켜야 할 규칙을 제공한다.
- Forge는 Unity Simulation Layer가 제공하는 GT를 그대로 라벨 포맷으로 투영하는 파이프라인이며, Annotation Stage가 이 역할을 담당한다. (별도 모델 비교가 필요하면 추가 Stage로 확장)

대상 범위:
- FrameBus → Capture → Annotation → Tracking → ReID → Occlusion → LabelAssembler → Encode → Storage → Edge Export → Validation/Stats/Manifest

---

## 2. 전체 파이프라인 개요 (Perception 기반)

### 2.0 Unity Perception 통합
Forge는 Unity Perception 패키지의 데이터 생성 파이프라인을 활용한다.
- **Blocking Capture**: 각 프레임 캡처가 완료될 때까지 시뮬레이션이 대기하므로, 별도의 복잡한 동기화 로직이 불필요하다.
- **Standard Format**: SOLO, COCO 등 표준화된 데이터셋 포맷을 사용하여 호환성을 높인다.

### 2.1 단계(Stage) - Perception Pipeline
1. **Simulation Step**: `ForgeScenario`가 다음 프레임으로 진행.
2. **Randomization**: `ForgeRandomizer`가 활성화되어 객체/카메라/조명 상태 변경.
3. **Rendering & Labeling**: `PerceptionCamera`가 렌더링하고, 부착된 `Labeler`들이 GT 생성.
4. **Serialization**: Perception `Consumer`가 데이터를 JSON/Binary로 직렬화.
5. **Storage**: `Endpoint`가 디스크에 저장.

---

## 3. ID 정책 (ID Policy)

### 3.1 Session ID
- `SessionConfig`의 `sessionId` 사용.

### 3.2 Frame ID
- Perception의 `step` (Iteration Count) 사용. 0부터 시작.

### 3.3 Camera ID
- `PerceptionCamera` 컴포넌트의 ID 또는 GameObject 이름 사용.

---

## 4. Perception 컴포넌트 상세

### 4.1 PerceptionCamera
- 역할: 이미지 캡처 및 라벨링 트리거.
- 설정:
  - **Capture Trigger Mode**: `Scheduled` (매 프레임) 또는 `Manual`.
  - **Description**: 데이터셋 메타데이터에 포함될 설명.

### 4.2 Labelers
- **BoundingBox2DLabeler**: 2D 객체 검출용 박스 생성.
- **SemanticSegmentationLabeler**: 픽셀 단위 객체 분류.
- **InstanceSegmentationLabeler**: 객체 인스턴스 식별.
- **KeypointLabeler**: 사람 관절 등 키포인트 검출.

### 4.3 Randomizers
- **ForgeRandomizer**: Forge 전용 커스텀 랜덤 로직 (카메라 배치 등).
- **Tag System**: `RandomizerTag`를 객체에 부착하여 랜덤화 대상 지정.

### 4.4 Output Format (SOLO)
- Unity Perception의 기본 출력 포맷.
- 단일 JSON 파일들의 시퀀스 또는 통합 파일.
- 구성:
  - `captures_000.json`: 프레임별 캡처 정보 (이미지 경로, 라벨 데이터).
  - `metrics_000.json`: 시뮬레이션 메트릭.
  - `annotation_definitions.json`: 라벨 정의 (클래스 이름, ID 등).

동시성:
- LabelAssembler Thread: 1 (권장)
- 품질 모드:
  - strict: Tracking 누락 + 타임아웃 시 세션 실패/PAUSE. 프레임 드롭 금지.
  - relaxed: 누락 프레임만 drop, `metrics.label.dropped_by_join_timeout_total` 및 `manifest.quality.droppedByJoinTimeout`에 기록.

**Join 로직 상세 구현 (구체화):**

```csharp
public class LabelAssembler {
    // Frame별 부분 데이터를 임시 저장
    private readonly ConcurrentDictionary<long, PartialFrameData> _pendingFrames = new();

    // 타임아웃 설정 (Config 가능)
    private readonly TimeSpan _joinTimeout = TimeSpan.FromSeconds(5);
    private readonly int _maxPendingFrames = 100; // 메모리 제한

    public async Task AssembleAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            // 주기적으로 타임아웃 체크 (100ms마다)
            await Task.Delay(100, ct);

            var now = DateTime.UtcNow;
            var completedFrames = new List<long>();

            foreach (var (frameId, partial) in _pendingFrames) {
                // 1. 모든 필수 데이터 도착 → 즉시 조립
                if (partial.HasTracking) {
                    var labeled = BuildLabeledFrame(partial);
                    OnLabeled?.Invoke(labeled);
                    completedFrames.Add(frameId);
                    continue;
                }

                // 2. 타임아웃 발생 → partial data로 진행
                if (now - partial.CreatedAt > _joinTimeout) {
                    _logger.LogWarning($"Frame {frameId} timeout after {_joinTimeout.TotalSeconds}s, " +
                                     $"assembling with partial data (hasTracking={partial.HasTracking})");

                    if (partial.HasTracking) {
                        var labeled = BuildLabeledFrame(partial);
                        OnLabeled?.Invoke(labeled);
                    } else {
                        _logger.LogError($"Frame {frameId} missing critical data (Tracking), skipping");
                    }

                    completedFrames.Add(frameId);
                }
            }

            // 완료된 프레임 정리
            foreach (var frameId in completedFrames) {
                _pendingFrames.TryRemove(frameId, out _);
            }

            // 3. 메모리 제한 초과 시 오래된 프레임 강제 제거
            if (_pendingFrames.Count > _maxPendingFrames) {
                var oldestFrames = _pendingFrames
                    .OrderBy(kvp => kvp.Value.CreatedAt)
                    .Take(_pendingFrames.Count - _maxPendingFrames)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var frameId in oldestFrames) {
                    _logger.LogError($"Frame {frameId} evicted (memory limit), data lost");
                    _pendingFrames.TryRemove(frameId, out _);
                }
            }
        }
    }

    public void OnTrackingReceived(TrackingData tracking) {
        var partial = _pendingFrames.GetOrAdd(tracking.FrameId, _ => new PartialFrameData {
            FrameId = tracking.FrameId,
            CreatedAt = DateTime.UtcNow
        });

        partial.Tracking = tracking;
        partial.HasTracking = true;
    }

    public void OnOcclusionReceived(List<OcclusionData> occlusion) {
        var frameId = occlusion.FirstOrDefault()?.FrameId ?? 0;
        if (frameId == 0) return;

        var partial = _pendingFrames.GetOrAdd(frameId, _ => new PartialFrameData {
            FrameId = frameId,
            CreatedAt = DateTime.UtcNow
        });

        partial.Occlusion = occlusion;
    }

    private LabeledFrame BuildLabeledFrame(PartialFrameData partial) {
        return new LabeledFrame {
            Frame = new FrameContext { FrameId = partial.FrameId },
            CameraLabels = new List<CameraLabelData> {
                new CameraLabelData {
                    Tracking = partial.Tracking?.ToRecords() ?? new List<TrackingRecord>(),
                    Occlusion = partial.Occlusion // nullable
                }
            }
        };
    }
}

class PartialFrameData {
    public long FrameId { get; set; }
    public DateTime CreatedAt { get; set; }

    public TrackingData Tracking { get; set; }
    public List<OcclusionData> Occlusion { get; set; }

    public bool HasTracking { get; set; }
}
```

**타임아웃 정책 (수치화):**

| 항목 | 값 | 설명 |
|------|------|------|
| **Join Timeout** | 5초 | Tracking 데이터 대기 최대 시간 |
| **Polling Interval** | 100ms | 타임아웃 체크 주기 |
| **Max Pending Frames** | 100 | 메모리 제한 (Frame별 평균 10KB 기준 1MB) |
| **필수 데이터** | TrackingData | 없으면 Frame skip |
| **선택 데이터** | OcclusionData | 없어도 진행 (Phase 2+) |
| **품질 모드 연동** | strict: timeout 시 세션 실패<br/>relaxed: drop+카운터 기록 | session.qualityMode 적용 |

**타임아웃 발생 시나리오:**

1. **OcclusionWorker 지연** (정상):
   - TrackingData 있음, OcclusionData 없음
   - → Occlusion 없이 LabeledFrame 생성 (경고 로그)

2. **TrackingWorker 장애** (비정상):
   - TrackingData 없음
   - → Frame 전체 skip (에러 로그)

3. **메모리 제한 초과**:
   - 100개 초과 Frame 대기 중
   - → 가장 오래된 Frame 강제 제거 (데이터 손실)

순서:
- frame_id 단위로 join 수행.
- 각 Stage 결과가 도착하는 시점이 다를 수 있으므로,
  타임아웃 내에 도착한 데이터만 조합.

에러 처리:
- 일부 데이터 누락(Occlusion 없는 경우):
  - 해당 필드 없이 LabeledFrame 생성 (optional 필드로 설계)
- TrackingData 자체 없음:
  - Frame 전체 skip (에러 로그)

---

### 4.8 Encode Stage – EncodeWorker

역할:
- RawImageData + LabeledFrame → 이미지/라벨 포맷으로 인코딩.

Input:
- LabeledFrame
- RawImageData[]

Output:
- EncodedFrame (이미지 bytes + 라벨 텍스트)

Queue:
- 입력 큐: `Queue<EncodeJob>`
- 기본 큐 크기: 2048

동시성:
- EncodeWorker Thread: 1~N
- 이미지/라벨 인코딩 분리 또는 배치 처리 가능.

에러 처리:
- 이미지 인코딩 실패:
  - 해당 frame의 이미지 누락, 라벨만 저장 옵션 or frame 전체 skip (설정)
- 라벨 포맷 변환 실패:
  - 해당 포맷만 건너뛰고 나머지 포맷 저장 (예: YOLO 변환 실패, JSON은 유지)

성능 목표:
- 디스크 I/O와 맞물리므로, 지나치게 무거운 인코딩 설정(고품질, 무손실 등)은 피한다.
- 기본 값은 JPG(중간 품질) + JSON 라벨.

---

### 4.9 Storage Stage – StorageWorker

역할:
- EncodedFrame의 이미지/라벨을 파일 시스템에 저장.

Input:
- EncodedFrame

Output:
- 실제 파일 (이미지/라벨)
- 성공/실패 결과 (StoredResult)

Queue:
- 입력 큐: `Queue<EncodedFrame>`
- 기본 큐 크기: 4096
- 디스크 I/O 병목 시 back-pressure 발생.

동시성:
- StorageWorker Thread: 1~N (기본 2~4)
- 동시 쓰기 수 제한을 둬서 디스크 thrash 방지.

에러 처리:
- 단일 파일 쓰기 실패:
  - N회 재시도 후 실패 시 해당 frame에 대한 저장 실패로 기록.
- 디스크 풀 또는 권한 오류:
  - 세션 즉시 중단 + 상태 “DiskFull/Error”로 마킹.
  - 사용자 알림 필수.

성능 목표:
- SSD 기준, 전체 pipeline FPS 목표를 만족할 수 있도록 thread/버퍼 크기 조절.
- 파일 open/close 최소화, 필요 시 batch / 버퍼링.

---

### 4.10 ReID Export Stage – ReIDExportWorker (Phase 2+)

역할:
- ReID 모델 학습에 필요한 person crop dataset을 export

Input:
- RawImageData
- TrackingData

Output:
- ReIDExportResult (저장된 crop 파일 경로, artifact status)

Queue:
- 입력 큐: `Queue<ReIDExportJob>`
- 기본 큐 크기: 1024

Export 디렉토리 구조:
```
/output/reid_dataset/
  <global_person_id>/
    <camera_id>/
      frame_<frame_id>.jpg
```

파일명 규칙:
- `frame_{frameId:06d}.jpg`

메타데이터:
- `metadata.csv` (필수 컬럼): `global_person_id,camera_id,frame_id,track_id,scene_name,occlusion,visibility,bbox_x,bbox_y,bbox_w,bbox_h,path`
- 필요 시 `metadata.json` 병행 생성 가능.

샘플링 정책:
- `reid.sample_interval` (기본 1 → 모든 frame)
- `reid.max_samples_per_person` (기본 0 → 제한 없음)

동시성:
- ReIDExportWorker Thread: 1~2 (I/O bound)

에러 처리:
- Crop 실패: 해당 person/frame skip, 로그 기록
- 디스크 공간 부족: 세션 중단
- Export 실패는 세션 실패로 보지 않고 `manifest.reidArtifacts[].status ∈ { "ok", "failed" }`로 기록

성능 목표:
- Export는 선택적 기능이므로 전체 파이프라인 FPS에 영향 최소화
- 필요 시 별도 세션으로 분리하여 post-processing으로 실행 가능

#### 4.10.1 Crop 정책
- 기본 bbox에서 상하 각 10%, 좌우 각 5%를 padding하여 인체를 충분히 포함한다. (`padTop=0.1`, `padBottom=0.1`, `padSides=0.05`)
- bbox가 이미지 경계를 넘어가면 zero-padding 대신 clamp 후 letterbox padding을 적용한다.
- 최소 해상도: 32×64. 이보다 작으면 sample drop 후 `manifest.reidArtifacts[].warnings`에 기록.
- Occlusion ratio가 `occlusion > reid.max_occlusion`(기본 0.8)인 경우 skip하여 노이즈를 줄인다.

#### 4.10.2 Aspect Ratio / Padding
- ReID 모델 기본 입력 비율은 2:1(Height:Width)로 정의한다. 설정값(`reid.aspectRatio`) 변경 가능.
- 처리 흐름:
  1. crop 영역을 원본 비율대로 잘라냄
  2. 타겟 사이즈(`reid.outputSize`, 기본 256×128)로 resize
  3. aspect ratio 불일치 시 letterbox padding(검정 또는 평균 색상)으로 채움
- padding 메타데이터(`pad_top`, `pad_bottom`, `pad_left`, `pad_right`)를 `metadata.csv`에 기록하여 후처리에서 재구성 가능하게 한다.

#### 4.10.3 Multi-camera / Global ID Consistency
- `global_person_id` 단위로 루트 디렉터리를 생성하고, 각 카메라별 서브 디렉터리를 둔다. 동일 인물이 여러 카메라에 등장해도 하나의 글로벌 폴더를 공유한다.
- `metadata.csv`에 `cross_camera_group_id`를 추가하여 동일 `global_person_id`가 포함된 카메라 리스트를 기록한다.
- 동일 프레임에서 여러 카메라 crop이 생성될 경우 `frame_{frameId}_{cameraId}.jpg` 형태로 중복 방지.
- Manifest에는 `reidArtifacts[].globalPersonId`, `cameraIds[]`, `sampleCount`를 기록하여 consistency 검증이 가능하도록 한다.

#### 4.10.4 Export Batching 및 파이프라인
- ReIDExportWorker는 `batch_size`(기본 64)만큼 crop 작업을 모아 GPU/CPU에서 일괄 처리한다.
  - Batch는 동일 카메라/해상도를 기준으로 묶어 resize 성능을 높인다.
  - `reid.max_batch_latency_ms`(기본 200ms)를 초과하면 현재까지 누적된 job을 즉시 flush한다.
- Flush 시:
  1. Batch 내 RawImageData를 순회하며 crop → resize → encode(JPEG/PNG configurable) 수행
  2. 파일 시스템에 연속 write 후 `metadata.csv` tail에 append
  3. `ReIDExportResult`에 batch 경로, 샘플 수, 실패 카운트를 기록해 Metrics/Manifest로 전파
- 실패한 crop은 batch 처리 후 별도 리스트로 반환하여 `metrics.reid.crop_failures_total` 증가.

#### 4.10.5 Config 항목 요약
```
"reid": {
  "enabled": true,
  "outputSize": [256, 128],
  "aspectRatio": 2.0,
  "padding": {"top":0.1,"bottom":0.1,"sides":0.05},
  "maxOcclusion": 0.8,
  "sampleInterval": 1,
  "maxSamplesPerPerson": 0,
  "batchSize": 64,
  "maxBatchLatencyMs": 200
}
```
- ConfigSchemaRegistry에 동일 항목을 등록하고, Validation에서 누락/범위 오류 발생 시 사용자를 차단한다.

---

### 4.11 Sensor Export Stage – SensorExportWorker (Phase 4+)

역할:
- Robotics Extension이 활성화된 세션에서 LiDAR/IMU/Wheel Odom/Depth/Trajectory와 SLAM Export(TUM/KITTI/Forge custom)를 생성한다.

Input:
- FrameContext (RobotPose, SensorMeta 포함)

Output:
- `sensors/` 디렉터리 내 센서별 파일
- `slam_export/tum/*`, `slam_export/kitti/*`
- Sensor quality 요약 (meta/sensorQuality.json)

Queue:
- SensorExportQueue (기본 512)

동시성:
- 1개 워커(기본) – 포맷별 순차 출력
- 대량 세션일 경우 Config로 2개까지 확장 가능

에러 처리:
- 파일 쓰기 실패 시 N회 재시도 후 실패 → manifest.robotics.sensors[].status=`failed`
- strict 품질모드에서는 실패 즉시 세션 종료

모니터링:
- `metrics.sensor_export.processed_frames`
- `metrics.sensor_export.failures_total`

Phase 4 기능이 비활성일 경우 SensorExportWorker는 등록되지 않는다.

---

### 4.12 Edge Export Stage – EdgeExportWorker (Phase 3+)

역할:
- Edge 디바이스 학습/추론 파이프라인을 위한 TFLite/ONNX/Custom Binary 라벨을 생성.
- EncodeWorker/StorageWorker가 생성한 데이터를 바탕으로 `.record`, `.npz`, `.bin`, `edge_manifest.json` 등을 만든다.
- 기본값은 `edge.export.enabled = false`이며, 활성화 시 `edge.export.formats[]`를 설정해야 한다.

Input:
- `EncodedFrame` 참조(이미지 bytes, 파일 경로)
- `LabeledFrame` 또는 `EdgeLabelSummary` (bbox, track_id, occlusion 등)
- SessionConfig의 `edgeExport` 섹션 (출력 포맷/모델/버전 정보)

Output:
- `EdgeExportArtifact`(포맷, 파일 경로, checksum)
- Session 종료 시 EdgeExportService로 전달되어 `edge_packages/{format}/`에 저장

Queue:
- 입력 큐: `Queue<EdgeExportJob>`
- 기본 큐 크기: 1024 (디폴트), 메모리 사용량에 따라 Config 조정

동시성:
- EdgeExportWorker Thread: 1 (GPU 기반 모델 변환 시) ~ N (단순 패키징 시)
- 포맷별 파이프라인을 분리해 병렬 처리 가능

에러 처리:
- 특정 포맷 실패 시 해당 artifact만 건너뛰고 경고 로그 남김.
- 모델 변환 실패/권한 오류/디스크 부족 발생 시 세션 상태에 경고를 추가하고 manifest `edgeArtifacts[].status` 필드를 `failed`로 설정.

디렉터리 구조:
```
edge_packages/
  tflite/
    data.record
    labels.bin
    tflite_manifest.json
  onnx/
    tensors.npz
    metadata.json
  visibility_binary/
    frame_000123.bin
```

manifest 확장:
```json
"edgeArtifacts": [
  {"format": "tflite-record", "path": "edge_packages/tflite/data.record", "checksum": "sha256:...", "specVersion": "1.0", "status": "ready"},
  {"format": "onnx-bundle", "path": "edge_packages/onnx/", "specVersion": "1.0", "status": "failed"}
]
```

기본 정책:
- 실패해도 세션은 유지하며, 해당 artifact만 `status="failed"`로 기록.
- `EdgeExportArtifact` 필수 필드: `format, path, checksum, specVersion, status`.

---

### 4.12 Post-processing – Validation / Stats / Manifest

역할:
- 세션 종료 후 정합성/품질/통계/메타데이터를 집계.

Input:
- SessionContext
- 생성된 파일들(디렉터리 스캔)

Output:
- ValidationReport
- DatasetStatistics
- manifest.json

수행 시점:
- Session 정상 종료 혹은 비정상 종료 후(가능한 범위 내에서) 실행.

에러 처리:
- 파일 일부 누락/손상 발견:
  - ValidationReport에 기록
  - manifest.json에 품질 경고 표시

---

## 5. Back-pressure & Queue 정책

### 5.1 기본 원칙

- 각 Stage는 입력 Queue 최대 크기를 가진다.
- Queue가 일정 수준을 넘으면 PipelineCoordinator가 back-pressure 신호를 생성한다.
- back-pressure 신호는 GenerationController에 전달되어:
  - FPS 감소
  - frame skip
  - 일시정지
  중 하나 또는 복합 전략을 사용하도록 한다.

### 5.2 Queue 임계값 예시

- CaptureQueue: 512
- AnnotationQueue: 512
- TrackingQueue: 2048
- OcclusionQueue: 1024
- LabelAssemblyQueue: 2048
- EncodeQueue: 2048
- StorageQueue: 4096
- ReIDExportQueue: 1024 (Phase 2+, optional)
- EdgeExportQueue: 1024 (Phase 3+, optional)
- SensorExportQueue: 512 (Phase 4+, robotics.enabled=true일 때만)

각 Stage별 임계값은 Config로 조정 가능.

### 5.3 back-pressure 행동 예시

- Level 1 (주의): 특정 Queue가 70% 이상 → 세션 FPS 10~20% 감소.
- Level 2 (경고): 90% 이상 → frame skip(예: N 프레임 건너뛰기).
- Level 3 (심각): 100% 지속 → 세션 일시정지, 사용자 알림.

---

## 6. 디렉토리/파일 구조

### 6.1 기본 디렉토리 구조

- `output_root/`
  - `session_{session_id}/`
    - `images/`
      - `cam01/`
        - `000000.jpg`
        - `000001.jpg`
        - ...
      - `cam02/`
        - ...
    - `labels/`
      - `json/`
        - `cam01/000000.json`
        - `cam01/000001.json`
        - ...
      - `yolo/` (Phase 2+)
      - `coco/` (Phase 2+)
    - `meta/`
      - `manifest.json`
      - `validation_report.json`
      - `stats.json` (선택 또는 manifest에 포함)
      - `sensorQuality.json` (Phase 4+, robotics.enabled=true일 때)
    - `camera_poses/`
      - `cam01.csv` (frame_id,timestamp,position_x,position_y,position_z,rotation_w,rotation_x,rotation_y,rotation_z,speed,rolling_shutter,motion_blur)
      - `cam02.csv`
    - `sensors/` (Phase 4+)
      - `lidar/`
      - `imu/`
      - `odom/`
      - `depth/`
      - `trajectory/`
    - `slam_export/` (Phase 4+)
      - `tum/`
      - `kitti/`
    - `edge_packages/` (Phase 3+)
      - `tflite/`, `onnx/`, `custom_binary/` 등 Config 기반 하위 디렉터리

### 6.2 파일명 규칙

- 이미지:
  - `{frame_id:06d}.jpg` 또는 `{frame_id:06d}.png`
- 라벨(JSON):
  - `{frame_id:06d}.json`
- 라벨(YOLO/COCO):
  - YOLO: 동일 파일명, 확장자 `.txt`
  - COCO: 세션 단위 `coco_annotations.json` 한 개도 가능 (옵션)

### 6.3 manifest.json 최소 필드

- version
- session_id
- created_at
- session_config 요약
- scene 목록
- camera 목록
- cameras[].type (`static`/`mobile`), `poseFile`(mobile일 경우 `camera_poses/camXX.csv`)
- camera pose 기록 여부 및 pose 파일 경로(`camera_poses/`)
- frame_count
- detection_count
- person_count
- validation_summary
- stats_summary
- edgeArtifacts (포맷, 경로, checksum, 상태)
- quality (예: `frameDropCount`, `droppedByJoinTimeout`, `stageStatusSummary`)
- reidArtifacts (status 포함, optional)
- robotics (Phase 4+):
  - `enabled`
  - `sensors[]` (각 센서 이름, status, outputPath, checksum)
  - `slamArtifacts[]` (포맷, 경로, checksum, status)
  - `sensorQualitySummary` (missingCount, latencyAvg, driftWarning)

---

## 7. 모니터링 / 메트릭

각 Worker는 `IWorkerMonitor` 인터페이스를 통해 다음 정보를 제공한다:

- 현재 Queue 길이
- 누적 처리 frame 수
- 평균 처리 시간(ms/frame)
- 실패 카운트
- 품질 관련 공통 메트릭:
  - `metrics.frame.dropped_total` (FrameBus drop)
  - `metrics.label.dropped_by_join_timeout_total` (LabelAssembler join timeout drop)
  - Stage별 성공/실패(Zero-Copy 시 StageStatuses 집계)

PipelineCoordinator는 이를 기반으로:

- ProgressInfo 계산
- Back-pressure 판단
- 로그/알람 트리거

ProgressReporter로 전달되는 정보:

- current_frame / total_frames
- overall FPS (최근 N초 평균)
- 예상 완료 시간
- 누적 에러/경고 수

### 7.1 Stage Failure Propagation Matrix

| Stage | Failure / Drop 조건 | Downstream 영향 | Manifest / Metrics 업데이트 |
|-------|--------------------|-----------------|-----------------------------|
| FrameBus | Queue overflow, Publish 실패 | Frame drop, GenerationController가 BackPressureLevel 상승 → IFrameRatePolicy가 Skip/Pause | `metrics.frame.dropped_total++`, `manifest.quality.frameDropCount`, Diagnostics `event=framebus_drop` |
| Capture | 특정 camera capture 실패 (N회) | 해당 camera RawImage 누락, AnnotationWorker는 빈 입력으로 처리 | `StageStatuses["Capture"]=Partial`, `manifest.quality.cameras[].missingFrames`, `metrics.capture.partial_total` |
| Capture | 모든 camera 실패 | frame skip, Join 단계까지 전달 안 됨 | Diagnostics severity=Error, `manifest.quality.frameDropByCapture++` |
| Annotation | GT 투영 오류/메타데이터 불일치 | DetectionData 빈 리스트 → TrackingWorker가 이전 상태 유지 | `StageStatuses["Annotation"]=Failed`, `manifest.quality.stageFailures.annotation++`, `metrics.annotation.failures_total` |
| Tracking | TrackingState 없음/유효성 실패 | LabelAssembler가 frame drop (strict) 또는 partial 라벨 (relaxed) | `manifest.quality.droppedByJoinTimeout` (LabelAssembler 기록), `StageStatuses["Tracking"]=Failed` |
| Occlusion | Visibility 데이터 누락 | LabeledFrame에 occlusion 필드 미포함, 나머지 pipeline은 계속 | `manifest.quality.stageWarnings.occlusion++` |
| LabelAssembler | Join timeout | strict: 세션 PAUSE, relaxed: frame drop | `metrics.label.dropped_by_join_timeout_total`, Diagnostics event, Manifest quality section에 누락 사유 기록 |
| Encode | 이미지/라벨 인코딩 실패 | 선택적 skip(포맷 단위) 또는 frame drop | `manifest.outputs[].status`, `metrics.encode.failures_total` |
| Storage | 디스크 오류/용량 없음 | 세션 즉시 중단 | `DiagnosticsService` critical event, Manifest `status="failed"` |
| ReID Export | Crop 실패 | Dataset 일부 누락 | `manifest.reidArtifacts[].status`, `metrics.reid.failures_total` |
| Edge Export | 아티팩트 제작 실패 | 해당 포맷만 failed | `edgeArtifacts[].status`, Diagnostics warning |
| Sensor Export | 파일 실패 | robotics. sensors[].status=failed | `metrics.sensor_export.failures_total`, Manifest robotics section |

### 7.2 StageStatus / Validation 연동 규칙

- Zero-Copy 모드에서는 `FramePipelineContext.StageStatuses`를 LabelAssembler가 집계하여 `StageStatusSummary`(성공/실패/부분 성공 카운터)를 생성하고 Manifest `quality.stageStatusSummary`에 저장한다.
- ValidationService는 StageStatuses를 입력 받아 다음을 수행한다:
- `CriticalStages = {Capture, Annotation, Tracking, Storage}` 중 하나라도 Failed가 있으면 ValidationReport에 `FatalIssues`로 기록.
  - Partial 상태(Occlusion, ReID 등)는 `Warnings`에 누적.
- DiagnosticsService는 StageStatus 변화 이벤트(예: Failed→Recovered)를 감시하여 `/status` health를 조정한다. critical Stage가 연속 실패하면 HTTP 503으로 전환한다.
- ProgressReporter는 StageStatuses/StageErrorFlags로부터 요약 문자열(예: `Tracking delayed (3 frames)`)을 생성해 CLI에 표시한다.

---

## 8. 성능/안정성 목표 요약

- 파이프라인은 **Frame 단위 비동기 처리**로 CPU/GPU/I/O 부하를 분산하고 Queue+Back-pressure로 메모리 폭주를 방지한다.
- Stage별 SLA를 명시적으로 관리하고 MetricsEmitter/DiagnosticsService와 연동해 목표를 정의한다.
- Validation/Stats/Manifest는 StageStatuses·품질 카운터를 기반으로 Dataset 품질을 보장한다.
- Config 기반으로 동시성/큐 크기/Export 설정을 튜닝하며, SLA를 만족하지 못할 경우 정책 전환(FrameRatePolicy, Zero-Copy 등)을 고려한다.

### 8.1 Stage별 SLA 테이블 (초기 목표)

| Stage | 목표 지표 | 초깃값 (1080p, 4 camera) | 측정 방법 | SLA 미달 시 대응 |
|-------|-----------|-------------------------|-----------|------------------|
| FrameBus | Publish/Consume latency | < 1ms | `metrics.framebus.latency_ms` 95p | Queue capacity 조정, lock contention 분석 |
| Capture | GPU→CPU Readback 시간 | < 12ms/frame | `metrics.capture.readback_ms` | AsyncGPUReadback 병렬 수 조절, 해상도 하향 |
| Annotation | Projection latency per frame | < 3ms | `metrics.annotation.projection_ms` 95p | 카메라/인원 수 조정, SIMD 최적화 |
| Tracking | per frame 처리 | < 3ms | `metrics.tracking.process_ms` | 알고리즘 단순화, thread 수 확대 |
| LabelAssembler | Join latency | < 5s timeout, 평균 < 200ms | `metrics.label.join_wait_ms` | `_joinTimeout` 조정, pending frame limit 확대 |
| Encode | 이미지+라벨 인코딩 | < 10ms/frame | `metrics.encode.process_ms` | 압축률 조정, SIMD/Native encoder 사용 |
| Storage | 파일 쓰기 | < 15ms/file | `metrics.storage.io_ms` | async I/O, disk throughput 점검 |
| ReID Export | crop 생성 | < 5ms/crop | `metrics.reid.process_ms` | 샘플링 비율 조정 |
| Edge Export | 포맷 생성 | < 500ms/batch | `metrics.edge_export.process_ms` | 배치 사이즈 조정, 별도 세션으로 분리 |

- SLA는 Phase 1 벤치마크 값을 기준으로 하며, Performance Benchmark 문서에 실제 측정치를 누적한다.
- MetricsEmitter는 Stage별 latency histogram을 Prometheus로 내보내고, SLA를 초과하는 경우 DiagnosticsService가 `event=stage_sla_violation`을 기록한다.
- Config에 `performance.targets` 섹션을 추가해 Stage별 목표를 재정의할 수 있고, Test Strategy 문서의 성능 회귀 테스트가 해당 값을 검증한다.
