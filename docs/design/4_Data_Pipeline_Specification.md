
## 1. 목적 (Purpose)

본 문서는 Forge의 **데이터 파이프라인**을  
구체적으로 정의한다.

- Stage(단계)별 입력/출력/큐/스레드/오류/백프레셔(back-pressure)를 명시한다.
- ID 정책(frame_id, camera_id, global_person_id 등)을 명시한다.
- 디렉터리/파일명/manifest 구조를 정의한다.
- 성능/안정성/운영 관점에서 구현 시 반드시 지켜야 할 규칙을 제공한다.

대상 범위:
- FrameBus → Capture → Detection → Tracking → ReID → Occlusion → LabelAssembler → Encode → Storage → Edge Export → Validation/Stats/Manifest

---

## 2. 전체 파이프라인 개요

### 2.0 메모리 소유권 및 생명주기 (Memory Ownership & Lifetime)

#### 2.0.1 기본 원칙

**원칙 1: 소유권 이전 (Move Semantics)**
- Stage 간 데이터 전달 시 **소유권이 이전**된다
- 이전 Stage는 데이터를 더 이상 참조하지 않는다
- 마지막 소비자가 메모리 해제 책임을 진다

**원칙 2: 불변성 (Immutability)**
- 기본 방식에서 각 Stage는 **새 객체를 생성**한다
- 이전 Stage의 데이터를 수정하지 않는다
- Thread-safety 보장

**원칙 3: ArrayPool 사용**
- 대용량 byte[] 버퍼는 `ArrayPool<byte>.Shared` 사용
- 마지막 소비자가 `Return()` 호출 책임

#### 2.0.2 객체별 소유권 규칙

| 객체 타입 | 생성자 | 초기 소유자 | 이전 경로 | 최종 소비자 | 해제 시점 |
|----------|--------|------------|-----------|------------|----------|
| `FrameContext` | GenerationController | FrameBus | CaptureWorker | CaptureWorker | Capture 완료 후 즉시 |
| `RawImageData` | CaptureWorker | DetectionWorker | TrackingWorker | DetectionWorker | Detection 완료 후 |
| `byte[] pixels` | CaptureWorker (Pool) | RawImageData | EncodeWorker | EncodeWorker | Encode 완료 후 Return |
| `DetectionData` | DetectionWorker | TrackingWorker | LabelAssembler | TrackingWorker | Tracking 완료 후 |
| `TrackingData` | TrackingWorker | LabelAssembler | - | LabelAssembler | Assembly 완료 후 |
| `OcclusionData` | OcclusionWorker | LabelAssembler | - | LabelAssembler | Assembly 완료 후 |
| `LabeledFrame` | LabelAssembler | EncodeWorker | StorageWorker | EncodeWorker | Encode 완료 후 |
| `EncodedFrame` | EncodeWorker | StorageWorker | - | StorageWorker | 저장 완료 후 |

#### 2.0.3 ArrayPool 사용 패턴

```csharp
// CaptureWorker.cs
public RawImageData CaptureImage(Camera camera)
{
    int pixelCount = width * height * 4;  // RGBA
    byte[] buffer = ArrayPool<byte>.Shared.Rent(pixelCount);

    try
    {
        // Unity에서 픽셀 읽기
        ReadPixels(camera, buffer);

        return new RawImageData
        {
            CameraId = camera.Id,
            FrameId = frameId,
            Pixels = buffer,  // ← 소유권 이전
            Width = width,
            Height = height,
            IsPooled = true  // ← Pool 사용 표시
        };
    }
    catch
    {
        // 예외 발생 시 즉시 반환
        ArrayPool<byte>.Shared.Return(buffer);
        throw;
    }
}

// EncodeWorker.cs
public EncodedFrame EncodeImage(RawImageData rawImage, LabeledFrame label)
{
    try
    {
        // 이미지 인코딩 (JPEG)
        byte[] jpegBytes = EncodeToJPEG(rawImage.Pixels, rawImage.Width, rawImage.Height);

        return new EncodedFrame
        {
            FrameId = rawImage.FrameId,
            ImageBytes = jpegBytes,
            Label = label
        };
    }
    finally
    {
        // EncodeWorker가 마지막 소비자 → Pool 반환
        if (rawImage.IsPooled)
        {
            ArrayPool<byte>.Shared.Return(rawImage.Pixels);
        }
    }
}
```

#### 2.0.4 Zero-Copy 방식의 소유권

Zero-Copy 방식에서는 **단일 FramePipelineContext** 객체가 파이프라인 전체를 통과한다.

```csharp
public class FramePipelineContext
{
    public FrameContext Frame { get; init; }
    public RawImageData[] Images { get; set; }
    public DetectionData Detection { get; set; }
    public TrackingData Tracking { get; set; }
    public LabeledFrame Label { get; set; }
    public EncodedFrame Encoded { get; set; }

    // Stage 상태 추적
    public Dictionary<string, StageStatus> StageStatuses { get; } = new();
}

// 각 Stage는 자신의 필드만 set (Idempotent Write)
public class DetectionWorker
{
    public void Process(FramePipelineContext context)
    {
        // Detection 수행
        var detections = DetectPersons(context.Images);

        // 한 번만 set
        context.Detection = detections;
        context.StageStatuses["Detection"] = StageStatus.Completed;

        // 이후 read-only로 취급
    }
}
```

**소유권 규칙**:
- `FramePipelineContext`는 **FrameBus가 생성**, **StorageWorker가 해제**
- 각 Stage는 자신이 set한 필드에 대해서만 **쓰기 권한** 보유
- 다른 Stage의 필드는 **읽기 전용**

#### 2.0.5 메모리 누수 방지 체크리스트

**개발 시 확인 사항**:
- [ ] ArrayPool에서 Rent한 버퍼를 반드시 Return 했는가?
- [ ] 예외 발생 시에도 Return이 호출되는가? (try-finally)
- [ ] RawImageData.IsPooled 플래그가 올바르게 설정되었는가?
- [ ] Worker 종료 시 Queue에 남은 객체를 모두 해제했는가?
- [ ] Event handler 등록 시 해제(unsubscribe)를 했는가?

**테스트 방법**:
```csharp
[Fact]
public void Pipeline_LongRunning_NoMemoryLeak()
{
    var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

    // 10,000 프레임 생성
    for (int i = 0; i < 10000; i++)
    {
        var frame = GenerateFrame(i);
        ProcessPipeline(frame);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
    var leaked = finalMemory - initialMemory;

    // 메모리 증가가 10% 이하여야 함
    Assert.True(leaked < initialMemory * 0.1,
        $"Memory leak detected: {leaked / 1024 / 1024} MB");
}
```

#### 2.0.6 Stage별 메모리 예산 (Phase 2+)

| Stage | 객체당 메모리 | Queue 크기 | 총 메모리 예산 |
|-------|-------------|-----------|--------------|
| FrameBus | 10 KB | 512 | ~5 MB |
| CaptureWorker | 8 MB (1080p RGBA) | 512 | ~4 GB |
| DetectionWorker | 100 KB | 512 | ~50 MB |
| TrackingWorker | 50 KB | 2048 | ~100 MB |
| LabelAssembler | 200 KB | 2048 | ~400 MB |
| EncodeWorker | 500 KB (JPEG) | 2048 | ~1 GB |
| StorageWorker | 500 KB | 4096 | ~2 GB |

**총 메모리 사용량 (최악)**:
```
이미지 버퍼: 4 GB
기타 데이터: 0.5 GB
시스템 오버헤드: 1 GB
─────────────────
합계: ~5.5 GB
```

**최적화 전략**:
- CaptureWorker 큐 크기 축소 (512 → 128)
- AsyncGPUReadback 사용하여 GPU → CPU 복사 지연
- ArrayPool 사용으로 할당 빈도 감소

---

### 2.1 단계(Stage)

1. Frame Dispatch (FrameBus)
2. Capture Stage (CaptureWorker)
3. Detection Stage (DetectionWorker)
4. Tracking Stage (TrackingWorker)
5. Occlusion Stage (OcclusionWorker, Phase 2+)
6. Label Assembly Stage (LabelAssembler)
7. Encode Stage (EncodeWorker)
8. Storage Stage (StorageWorker)
9. ReID Export Stage (ReIDExportWorker, Phase 2+)
10. Edge Export Stage (EdgeExportWorker, Phase 3+)
11. Post-processing (ValidationService / StatsService / ManifestService)

각 Stage는 **Worker + 입력 Queue**로 구성되며,  
Stage 간에는 데이터 모델(DataModel v2)을 통해 전달된다.

### 2.2 데이터 모델 흐름

**기본 방식 (Phase 1-2):**
- FrameContext (+ CameraMeta)
  → RawImageData[]
  → DetectionData
  → TrackingData
  → OcclusionData (optional)
  → LabeledFrame
  → EncodedFrame
  → Files (이미지/라벨) + EdgeExportArtifacts (옵션)

각 Stage가 새 객체 생성 (불변성 보장, Thread-safe)

**Zero-Copy 방식 (Phase 2+ 옵션):**
```csharp
class FramePipelineContext {
    public FrameContext Frame { get; init; }
    public RawImageData[] Images { get; set; }
    public DetectionData Detection { get; set; }
    public TrackingData Tracking { get; set; }
    public LabeledFrame Label { get; set; }
    public EncodedFrame Encoded { get; set; }
    public IReadOnlyList<CameraPose> CameraPoses { get; set; }
}

// 단일 FramePipelineContext 객체가 파이프라인 전체를 통과
// 메모리 복사 최소화, GC 부담 감소
```

**Trade-off:**
| 항목 | 기본 방식 | Zero-Copy 방식 |
|------|----------|---------------|
| 메모리 사용 | 높음 (복사 발생) | 낮음 (재사용) |
| Thread-safety | 높음 (불변 객체) | 낮음 (공유 상태) |
| 디버깅 | 쉬움 (각 Stage 분리) | 어려움 (상태 추적 복잡) |
| 적용 시점 | Phase 1 기본 | Phase 2+ 성능 측정 후 |

**선택 기준:**
```
IF (메모리 사용량 > 8GB AND GC 시간 > 10%)
THEN Zero-Copy 방식 고려
ELSE 기본 방식 유지 (단순성 우선)
```

**Zero-Copy 제약/에러 보고 (Experimental, 기본 OFF):**
- Zero-Copy는 실험 옵션이며 기본 비활성화한다.
- `FramePipelineContext`에 `StageStatuses`(Dictionary<string, StageStatus>)와 `StageErrorFlags`를 추가해 Stage별 완료/실패를 기록한다.
- 각 Stage는 자신에게 할당된 필드를 **한 번만 set**(idempotent write)하고 이후 read-only로 취급한다.
- Stage에서 예외/누락 발생 시 `StageStatuses["Tracking"]=Failed` 식으로 플래그를 남기고, Validation/Manifest가 StageStatuses를 집계해 품질 보고에 반영한다.

**별도 흐름 (ReID Export):**
- RawImageData + TrackingData → ReID Crop Images (person_id 기반 디렉토리)
- 이동형 카메라 pose:
  - FrameBus Publish 시 `FrameContext`에 `CameraPoses`(camera_id, extrinsic, position, rotation, timestamp)를 포함한다.
  - CaptureWorker는 `FramePipelineContext.CameraPoses`에 pose 리스트를 그대로 전달한다.
  - LabelAssembler는 LabeledFrame/ManifestStage가 pose 정보를 참조할 수 있도록 `LabeledFrame.CameraPoses` 또는 별도 DTO에 포함한다.
  - ManifestWriter는 pose 데이터를 `meta/camera_poses/{camera_id}.csv` 또는 `manifest.cameras[].poses`에 직렬화한다. 필수 컬럼: frame_id, timestamp, position(x,y,z), rotation(quaternion), speed(optional).

### 2.3 Frame Generation Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant App as Generation<br/>Command
    participant Session as Session<br/>Manager
    participant GenCtrl as Generation<br/>Controller
    participant Sim as Simulation<br/>Layer
    participant Bus as FrameBus
    participant Capture as Capture<br/>Worker
    participant Detection as Detection<br/>Worker
    participant Tracking as Tracking<br/>Worker
    participant Storage as Storage<br/>Worker
    participant PipeCoord as Pipeline<br/>Coordinator

    User->>App: start session
    App->>Session: CreateSession(config)
    Session->>Session: create SessionContext<br/>setup output directory
    Session->>GenCtrl: Initialize(session)
    GenCtrl->>Sim: setup scene/cameras/crowd
    activate GenCtrl

    rect rgb(240, 248, 255)
        Note over GenCtrl,Storage: Frame Generation Loop
        loop for each frame (0 to totalFrames)
            GenCtrl->>PipeCoord: check back-pressure
            PipeCoord-->>GenCtrl: OK / SLOW / PAUSE

            alt back-pressure OK
                GenCtrl->>Sim: Update(deltaTime)
                Sim->>Sim: update crowd<br/>update behaviors<br/>update time/weather
                Sim-->>GenCtrl: ready

                GenCtrl->>GenCtrl: create FrameContext<br/>(frameId, timestamp, scene)
                GenCtrl->>Bus: Publish(frameContext, cameras)

                Bus->>Capture: enqueue FrameCaptureJob
                activate Capture
                Capture->>Sim: ReadPixels(camera)
                Sim-->>Capture: pixels
                Capture->>Capture: create RawImageData
                Capture->>Detection: enqueue RawImageData
                deactivate Capture

                activate Detection
                Detection->>Detection: run person detection
                Detection->>Detection: create DetectionData
                Detection->>Tracking: enqueue DetectionData
                deactivate Detection

                activate Tracking
                Tracking->>Tracking: update tracks<br/>assign trackId
                Tracking->>Tracking: create TrackingData
                Tracking->>Storage: enqueue (via pipeline)
                deactivate Tracking

                activate Storage
                Storage->>Storage: encode image/label
                Storage->>Storage: write to disk
                Storage-->>PipeCoord: update metrics
                deactivate Storage

            else back-pressure SLOW
                GenCtrl->>GenCtrl: reduce FPS
                Note right of GenCtrl: Skip frames or<br/>slow down generation

            else back-pressure PAUSE
                GenCtrl->>GenCtrl: pause generation
                GenCtrl->>PipeCoord: wait for queues to drain
                PipeCoord-->>GenCtrl: resume OK
            end

            GenCtrl->>App: report progress<br/>(currentFrame, FPS, ETA)
            App->>User: display progress
        end
    end

    deactivate GenCtrl

    rect rgb(255, 248, 240)
        Note over Session,Storage: Session Completion
        GenCtrl->>Session: notify complete
        Session->>Storage: wait for all pending frames
        Storage-->>Session: all stored
        Session->>Session: run Validation/Stats/Manifest
        Session-->>App: session complete
        App->>User: show summary
    end
```

### 2.4 품질 모드 (Quality Mode)

`session.qualityMode ∈ { strict, relaxed }`

- **strict**: 프레임 드롭이 발생하면 세션을 PAUSE/FAIL 처리. FrameBus/LabelAssembler 등에서 drop 발생 시 GenerationController가 일시정지 후 재시도, 회복 불가 시 세션 실패로 간주. 최종 manifest/validation에서 frameDropCount > 0이면 invalid.
- **relaxed**: 프레임 드롭 허용. 드롭 수치는 `metrics.frame.dropped_total`, `manifest.quality.frameDropCount` 등으로 집계하여 투명하게 노출. 기본 모드.

---

## 3. ID 정책 (ID Policy)

### 3.1 Session ID

- `session_id`는 세션 단위로 유일한 문자열.
- Config에 명시되지 않을 경우, UUID 또는 날짜 기반으로 생성.

### 3.2 Frame ID

- `frame_id`는 세션 내에서 0부터 시작하는 증가형 정수.
- 모든 Stage에서 frame_id는 변경되지 않는다.
- FrameContext에 포함되며, 파이프라인 전 구간에서 ID의 기준이 된다.

### 3.3 Camera ID

- `camera_id`는 Config에서 정의하는 문자열(예: "cam01", "cam02").
- 세션 전체에서 동일 카메라는 동일 camera_id를 유지.
- 디렉터리 및 파일명에 camera_id를 포함해 사용한다.

### 3.4 Global Person ID

- `global_person_id`는 Session 전체에서 동일 인물에 대해 불변인 정수.
- CrowdService 초기화 시 할당하거나 Agent 생성 시 부여.
- Tracking/ReID/Occlusion 전 단계를 통틀어 동일 ID를 유지해야 한다.

### 3.5 Track ID

- `track_id`는 (session_id, camera_id) 범위 내에서 유일한 정수.
- 각 카메라별로 독립적인 Track ID 시퀀스를 가진다.
- 동일 카메라 내에서 같은 인물(같은 global_person_id)은 같은 track_id를 유지하는 것이 기본 정책.
  (끊겼다가 다시 등장하는 경우 정책은 TrackingWorker에서 정의.)

---

## 4. Stage별 상세 스펙

각 Stage는 아래 정보를 가진다:

- Input
- Output
- Queue / 동시성
- 순서 보장 정책
- 에러 처리
- 성능 목표(지향)

---

### 4.1 Frame Dispatch – FrameBus

역할:
- Simulation(Main Thread)에서 생성된 FrameContext + Camera 메타를 파이프라인으로 전달.

Input:
- FrameContext (session_id, frame_id, sceneName, timestamp 등)
- Active Cameras (SimCamera 리스트 또는 CameraMeta 리스트)

Output:
- CaptureWorker 입력 큐에 전달될 메시지:
  - FrameContext
  - CameraMeta 리스트

Queue:
- 내부적으로 thread-safe queue 사용.
- Publish: Main Thread (GenerationController)
- Consume: CaptureWorker Thread
- 품질 모드 반영:
  - strict: `Publish` 실패(큐 초과) 시 GenerationController에 PAUSE/FAIL 신호. 드롭 금지.
  - relaxed: `Publish` 실패 시 해당 frame drop, `metrics.frame.dropped_total` 및 `manifest.quality.frameDropCount`에 누적.

**Thread-safety 구현 (구체화):**

```csharp
public class FrameBus : IFrameBus {
    // Thread-safe queue (lock-free)
    private readonly ConcurrentQueue<FrameJob> _queue = new();

    // Semaphore for blocking wait (메모리 효율)
    private readonly SemaphoreSlim _signal = new(0);

    // Cancellation for graceful shutdown
    private readonly CancellationTokenSource _cts = new();

    // Back-pressure tracking
    private int _queueLength = 0;
    private const int QUEUE_CAPACITY = 512;

    // Unity Main Thread에서 호출
    public bool Publish(FrameContext frame, List<CameraMeta> cameras) {
        // Back-pressure 체크
        var currentLength = Interlocked.Increment(ref _queueLength);

        if (currentLength > QUEUE_CAPACITY) {
            Interlocked.Decrement(ref _queueLength);
            _logger.LogWarning($"FrameBus queue full ({QUEUE_CAPACITY}), dropping frame {frame.FrameId}");
            return false;
        }

        var job = new FrameJob {
            Frame = frame,
            Cameras = cameras,
            PublishedAt = DateTime.UtcNow
        };

        _queue.Enqueue(job);
        _signal.Release(); // Worker 쓰레드 깨우기

        return true;
    }

    // Worker Thread에서 호출
    public async Task<FrameJob> ConsumeAsync(CancellationToken ct) {
        // Semaphore 대기 (CPU 점유 없이 blocking)
        await _signal.WaitAsync(ct);

        if (_queue.TryDequeue(out var job)) {
            Interlocked.Decrement(ref _queueLength);

            // 대기 시간 모니터링
            var latency = DateTime.UtcNow - job.PublishedAt;
            if (latency > TimeSpan.FromSeconds(1)) {
                _logger.LogWarning($"Frame {job.Frame.FrameId} latency: {latency.TotalMilliseconds}ms");
            }

            return job;
        }

        throw new InvalidOperationException("Semaphore released but queue empty (race condition)");
    }

    // 현재 큐 길이 (모니터링용)
    public int GetQueueLength() => _queueLength;

    // Graceful shutdown
    public async Task Shutdown() {
        _cts.Cancel();

        // 남은 작업 처리 대기 (최대 5초)
        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        while (_queueLength > 0 && !timeout.IsCompleted) {
            await Task.Delay(100);
        }

        _logger.LogInfo($"FrameBus shutdown, {_queueLength} frames dropped");
    }
}
```

**동시성 안전성 분석:**

| 항목 | 전략 | 이유 |
|------|------|------|
| **Queue 구조** | `ConcurrentQueue<T>` | Lock-free, MPSC(Multi-Producer-Single-Consumer) 최적화 |
| **Blocking** | `SemaphoreSlim` | `Thread.Sleep` 대비 CPU 점유 최소화 |
| **Counter** | `Interlocked` | Atomic 연산으로 정확한 큐 길이 추적 |
| **Back-pressure** | Capacity 초과 시 Publish 거부 | OOM 방지 |

순서:
- FrameContext는 frame_id 순으로 publish된다.
- CaptureWorker는 frame_id 순서를 **가능한 유지**하지만,
  파이프라인은 전체적으로 "frame 단위 작업 완료 순서"에 제한을 두지 않는다.

에러:
- FrameBus에서 에러 발생 시(메모리 부족 등), 해당 frame을 drop하고 경고 로그 남김.
- 치명적 에러 시 세션 중단.

성능 목표:
- FrameBus 자체는 병목이 되지 않을 것 (복잡한 연산 금지).
- Publish/Consume 각각 < 1ms (lock contention 최소화)

---

### 4.2 Capture Stage – CaptureWorker

역할:
- 각 카메라의 이미지 캡처 → RawImageData 생성.
- Unity API 렌더/ReadPixels는 메인/렌더 스레드 제약을 받으므로, CaptureWorker는 **메인 스레드에서 준비된 버퍼(동기 캡처 또는 AsyncGPUReadback 결과)**만 소비한다.

Input:
- FrameContext
- CameraMeta 리스트

Output:
- RawImageData[] (각 camera_id별 1개)

Queue:
- 입력 큐: `Queue<FrameCaptureJob>`
- 큐 크기 제한: 기본 512 Frame (설정 가능)
- 큐가 가득 찰 경우:
  - PipelineCoordinator에 back-pressure 신호 전달

동시성:
- Phase 1: 메인 스레드 동기 캡처(카메라 렌더 + ReadPixels). CaptureWorker는 이미 채워진 byte[]/NativeArray를 감싸는 역할만 수행.
- Phase 2: AsyncGPUReadback 사용. 메인 스레드 콜백에서 GPU→CPU 복사 완료 후 Worker 큐에 push. 필요 시 2개 이상으로 확장 가능하나 Unity Render/Readback 제약 고려.

순서:
- 입력 frame_id 순서로 처리되도록 노력하되, 타 Stage는 순서에 의존하지 않도록 설계.

에러 처리:
- 개별 카메라 캡처 실패:
  - 최대 N회 재시도 (기본 3회)
  - 실패 시 해당 카메라 이미지는 누락, 로그 기록
- 모든 카메라 캡처 실패:
  - 해당 frame 전체를 skip
- 반복적 실패(연속 X 프레임 이상) 시 세션 중단 옵션(설정) 제공.

성능 목표:
- 카메라 수 3~6 기준, 전체 파이프라인 FPS 목표를 만족하도록
  이미지 캡처 시간(frame당) 최소화.

---

### 4.3 Detection Stage – DetectionWorker

역할:
- RawImageData에 대해 사람 bbox + confidence 생성.

Input:
- RawImageData[]

Output:
- DetectionData (frame_id, camera_id별 detection 리스트)

Queue:
- 입력 큐: `Queue<RawImageData[]>`
- 기본 큐 크기: 512
- 큐 풀 시:
  - CaptureWorker로 back-pressure: 더 이상 enqueue하지 않고 대기.

동시성:
- DetectionWorker Thread: 1~N (configurable)
- Stage 내부에서 batch 처리 가능 (예: 여러 frame을 한 번에 inference).

순서:
- Frame별 독립 처리. 순서 유지 필요 없음.

에러 처리:
- 모델 inference 실패:
  - 해당 frame에 대해 detection 없음으로 처리 (빈 리스트)
  - 경고 로그
- 반복 실패 시(모든 frame detection 실패 지속) 세션 중단 가능.

성능 목표:
- 1080p 기준 카메라 N개 합산 FPS가 전체 목표 FPS(예: 5~15 FPS)에 걸리지 않도록 모델/설정 선택.

#### 4.3-A Inference Engine Integration (GPU/CPU 경계)

- DetectionWorker는 `IInferenceRunner` 추상화를 통해 ONNX Runtime, TensorRT, TorchScript 등 다양한 엔진을 플러그인 방식으로 사용한다.
- 기본 배치 전략:
  1. CaptureWorker가 ArrayPool 버퍼를 채운 RawImageData를 DetectionWorker 큐에 push
  2. DetectionWorker는 `IImagePreprocessor`가 GPU 텍스처/CPU 메모리 전환을 담당하도록 위임
  3. `IInferenceRunner.RunAsync(Span<byte> batchTensor, InferenceContext ctx)` 호출 시 GPU 스트림 ID와 batch 크기를 명시
  4. GPU 결과는 `DetectionParser`가 즉시 CPU 구조체로 변환하여 다음 Stage에 전달
- GPU/CPU 간 메모리 전환 규칙:
  - AsyncGPUReadback 사용 시 GPU→CPU 복사는 Capture 단계에서 완료하고 DetectionWorker는 CPU 메모리만 읽는다.
  - TensorRT/Torch RT 등의 GPU Inference를 사용할 경우 RawImageData를 GPU 텍스처로 재업로드하지 않도록, Capture 단계에서 GPU native handle을 함께 제공하는 옵션(`RawImageData.GpuHandle`)을 두고 DetectionWorker가 선택적으로 활용한다.
- Thread/Stream 설계:
  - `DetectionWorker`는 CPU 측 Stage지만, 내부에서 GPU 스트림을 사용해 비동기 실행.
  - `detection.inference.concurrentStreams` 설정으로 GPU 스트림 수를 제어하여 AsyncGPUReadback, TrackingWorker와 경합을 피한다.
- 장애/핫스왑:
  - `IInferenceRunnerHealthCheck`가 주기적으로 성능/오류율을 MetricsEmitter에 기록하고, 임계치 초과 시 fallback 엔진(예: CPU)으로 스위칭한다.
  - 스위칭 이벤트는 DiagnosticsService에 `type="inference_failover"`로 기록하고 Manifest `quality.inferenceFallbackCount`에 누적한다.
- 문서 연계:
  - GPU/Inference 구성 파라미터는 `SessionConfig.detection.inference` 섹션에 정의하고 ConfigSchemaRegistry에서 검증한다.

---

### 4.4 Tracking Stage – TrackingWorker

역할:
- Detection 결과 기반으로 카메라별 Tracking 수행.
- Simulation Layer의 PersonState와 매칭하여 Global Person ID 할당

Input:
- DetectionData
- FrameContext (PersonState 포함)

Output:
- TrackingData (track_id + global_person_id 포함)

Queue:
- 입력 큐: `Queue<DetectionData>`
- 기본 큐 크기: 2048

동시성:
- TrackingWorker Thread: 1 (기본), 필요 시 카메라별 분리 가능.

순서:
- 같은 camera_id에 대해서는 frame_id 순서를 보장하는 것이 이상적.
- 구현 상 out-of-order 발생 시, 유실/재정렬 정책은 TrackingWorker 내부에서 정의.

에러 처리:
- 특정 frame tracking 실패:
  - 이전 frame 상태 기반 계속 진행하거나, 해당 frame만 detection-only 취급.
- 상태 초기화 필요 시:
  - 해당 카메라의 track state를 reset하고 이후 frame에서 새 track_id 생성.

성능 목표:
- Detection보다 가볍게 유지.
- Tracking 연산이 전체 파이프라인의 병목이 되지 않도록 설계.

---

### 4.5 Occlusion Stage – OcclusionWorker (Phase 2+)

역할:
- VisibilityService meta + TrackingData 기반으로 occlusion/visibility 계산.

Input:
- VisibilityMeta (Simulation layer에서 제공)
- TrackingData

Output:
- OcclusionData 리스트

Queue:
- 입력 큐: `Queue<OcclusionJob>`
- 기본 큐 크기: 1024

동시성:
- OcclusionWorker Thread: 1 (기본)

에러 처리:
- 계산 실패 시 해당 객체의 occlusion/visibility를 기본값(예: 0 또는 null)로 두고 경고 로그.

성능 목표:
- Depth/Geometry 계산 정책에 따라 적절히 튜닝.
- 파이프라인 병목이 되지 않아야 한다.

---

### 4.6 Label Assembly Stage – LabelAssembler

역할:
- TrackingData + OcclusionData를 하나의 LabeledFrame으로 조합.

Input:
- TrackingData
- OcclusionData (optional)

Output:
- LabeledFrame

Queue:
- 입력 큐: `Queue<LabelAssemblyJob>`
- 기본 큐 크기: 2048

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
- DetectionQueue: 512
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
| Capture | 특정 camera capture 실패 (N회) | 해당 camera RawImage 누락, DetectionWorker는 빈 입력으로 처리 | `StageStatuses["Capture"]=Partial`, `manifest.quality.cameras[].missingFrames`, `metrics.capture.partial_total` |
| Capture | 모든 camera 실패 | frame skip, Join 단계까지 전달 안 됨 | Diagnostics severity=Error, `manifest.quality.frameDropByCapture++` |
| Detection | Inference 오류/timeout | DetectionData 빈 리스트 → TrackingWorker가 이전 상태 유지 | `StageStatuses["Detection"]=Failed`, `manifest.quality.stageFailures.detection++`, `metrics.detection.failures_total` |
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
  - `CriticalStages = {Capture, Detection, Tracking, Storage}` 중 하나라도 Failed가 있으면 ValidationReport에 `FatalIssues`로 기록.
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
| Detection | Inference latency per batch | < 25ms (YOLOv8n, batch=4) | `metrics.detection.inference_ms` 95p | 다른 모델/엔진 선택, batch 크기 조정 |
| Tracking | per frame 처리 | < 3ms | `metrics.tracking.process_ms` | 알고리즘 단순화, thread 수 확대 |
| LabelAssembler | Join latency | < 5s timeout, 평균 < 200ms | `metrics.label.join_wait_ms` | `_joinTimeout` 조정, pending frame limit 확대 |
| Encode | 이미지+라벨 인코딩 | < 10ms/frame | `metrics.encode.process_ms` | 압축률 조정, SIMD/Native encoder 사용 |
| Storage | 파일 쓰기 | < 15ms/file | `metrics.storage.io_ms` | async I/O, disk throughput 점검 |
| ReID Export | crop 생성 | < 5ms/crop | `metrics.reid.process_ms` | 샘플링 비율 조정 |
| Edge Export | 포맷 생성 | < 500ms/batch | `metrics.edge_export.process_ms` | 배치 사이즈 조정, 별도 세션으로 분리 |

- SLA는 Phase 1 벤치마크 값을 기준으로 하며, Performance Benchmark 문서에 실제 측정치를 누적한다.
- MetricsEmitter는 Stage별 latency histogram을 Prometheus로 내보내고, SLA를 초과하는 경우 DiagnosticsService가 `event=stage_sla_violation`을 기록한다.
- Config에 `performance.targets` 섹션을 추가해 Stage별 목표를 재정의할 수 있고, Test Strategy 문서의 성능 회귀 테스트가 해당 값을 검증한다.
