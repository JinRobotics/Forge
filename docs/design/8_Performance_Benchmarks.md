
## 1. 목적

Forge의 성능 목표를 정의하고,
측정 방법, 도구, 벤치마크 시나리오를 명시한다.

---

## 2. 성능 목표

### 2.1 Phase별 FPS 목표

| Phase | FPS 목표 | Latency (frame당) | 근거 |
|-------|----------|-------------------|------|
| Phase 1 | 5~10 FPS | <200ms | 기본 파이프라인, 단일 스레드 |
| Phase 2 | 15~30 FPS | <100ms | Worker 병렬화, AsyncGPU |
| Phase 3 | 30~60 FPS | <50ms | 최적화, Multi-GPU 고려 |

**FPS 계산**:
```
FPS = 완료된 프레임 수 / 경과 시간(초)

예: 100,000 프레임을 3시간(10,800초)에 생성
→ 100,000 / 10,800 = 9.26 FPS
```

### 2.2 리소스 사용 목표

| 메트릭 | Phase 1 | Phase 2 | Phase 3 |
|--------|---------|---------|---------|
| CPU 사용률 | <60% | <80% | <90% |
| GPU 사용률 | <70% | <85% | <95% |
| 메모리 사용 | <4GB | <8GB | <16GB |
| 디스크 I/O | <100MB/s | <200MB/s | <500MB/s |

StorageWorker는 Phase 3(1M+ 프레임) 기준으로 **지속 쓰기 400MB/s 이상**을 목표로 하며, NVMe SSD 기준 70% 이상의 time-in-write가 발생하면 Worker 동시성을 늘리거나 압축 품질을 조정한다. 대용량 세션에서는 `storage.bandwidthTargetMBps` 설정값을 통해 워커가 예상 스루풋을 넘기지 못할 경우 경고를 발생시키고, `/status.metricsSummary.storageThroughput`에 실제 MB/s를 노출한다.

### 2.3 안정성 목표

- **12시간 연속 실행**: 메모리 누수 <10% 증가
- **파일 손상률**: <0.01% (100개/1M 프레임)
- **라벨 정확도**: 100% (GT이므로 항상 정확)

### 2.4 벤치마크 환경 (Baseline Hardware)

| Phase | CPU | GPU | RAM | Storage | 비고 |
|-------|-----|-----|-----|---------|------|
| Phase 1 | 6C12T (Ryzen 5 5600) | RTX 3060 12GB | 32GB | NVMe SSD 1GB/s | Studio/개발자 PC 기준 |
| Phase 2 | 8C16T (Ryzen 7 5800X) | RTX 3080 10GB | 32GB | NVMe SSD 2GB/s | AsyncGPU/6 camera 테스트 |
| Phase 3 | 16C32T (Threadripper PRO 5955WX) | RTX 4090 24GB + RTX 3080* | 64GB | NVMe RAID 0 (4GB/s) | Multi-GPU, 대규모 세션 |
| Phase 4 (Robotics) | 24C48T 서버급 | RTX 4090 + NVIDIA L40 (Isaac) | 128GB | NVMe + 10GbE NAS | Unity + Isaac 동시 실행 |

- 모든 측정은 Ubuntu 22.04 LTS, .NET 8.0, Unity 2023 LTS 기준으로 수행한다.
- CPU governor는 `performance`, GPU는 최대 전력 모드로 고정한다.
- 하드웨어 업데이트가 필요한 경우 본 표와 7_Test_Strategy의 성능 섹션을 동시에 갱신한다.

---

## 3. 벤치마크 시나리오

### 3.1 Scenario 1: Baseline (Phase 1)

**환경**:
- Scene: Factory
- Cameras: 3 (1920x1080)
- Crowd: 20~30 people
- Duration: 10,000 frames

**목표**:
- FPS: 5~10
- 완료 시간: <30분

**측정 항목**:
- 실제 FPS
- 메모리 사용량 (시작/종료)
- CPU/GPU 사용률 (평균/최대)
- 디스크 쓰기 속도

**실행 방법**:
```bash
dotnet run --project src/Application -- \
  --config tests/performance/configs/baseline.json \
  --output /tmp/perf_baseline

# 결과 분석
python tools/analyze_performance.py /tmp/perf_baseline/meta/manifest.json
```

### 3.2 Scenario 2: Multi-Camera (Phase 2)

**환경**:
- Scene: Factory + Office (전환 1회)
- Cameras: 6 (1920x1080)
- Crowd: 50~80 people
- Duration: 50,000 frames

**목표**:
- FPS: 15~30
- 완료 시간: <1.5시간

**측정 항목**:
- Pipeline Stage별 처리 시간
- Queue depth (각 Worker)
- Back-pressure 발생 횟수
- Scene 전환 오버헤드

### 3.3 Scenario 3: Large Scale (Phase 3)

**환경**:
- Scene: Factory + Office + Warehouse
- Cameras: 6 (1920x1080)
- Crowd: 80~150 people
- Duration: 1,000,000 frames

**목표**:
- FPS: 30~60
- 완료 시간: <7시간

**측정 항목**:
- 장시간 실행 시 성능 저하 여부
- 메모리 누수 탐지
- 디스크 공간 사용량 추이
- 총 데이터셋 크기

### 3.4 Scenario 4: Stress Test (Phase 3)

**환경**:
- Scene: All scenes
- Cameras: 6 (최대 해상도)
- Crowd: 200+ people
- Duration: 10,000 frames

**목표**:
- 시스템 한계 측정
- 최대 FPS 달성
- 최대 안정 인원 수

**측정 항목**:
- 최대 FPS
- 최대 동시 인원 수 (안정적)
- GPU/CPU 병목 지점
- Queue overflow 발생 조건

### 3.5 Scenario 5: Distributed Dual-Sim (Phase 4)

**환경**:
- Master 노드: 16C32T CPU, RTX 4090
- Worker 노드(2대): 12C24T CPU, RTX 3080, 10GbE 네트워크
- Simulation: Unity + Isaac (IRoboticsGateway) 동시 구동
- 네트워크 지연 시뮬레이션: RTT 1ms / 5ms / 20ms (tc netem)
- Duration: 200,000 frames

**목표**:
- FPS: 20~30 (RTT 1ms), 15~25 (5ms), 10~18 (20ms)
- Back-pressure로 인한 Skip 비율: <3%
- MultiSimSyncCoordinator의 `sync_offset_ms` 평균 <2ms, 99p <5ms

**측정 항목**:
- Worker별 FPS / queue_ratio / GPU 사용률
- 네트워크 RTT별 Frame drop, sync timeout 횟수
- Master ↔ Worker HTTP/gRPC latency (p50/p95)
- Isaac 지연/장애 주입 시 FAIL/Skip 정책 동작

**실행 방법**:
```bash
./scripts/run_distributed_benchmark.sh \
  --workers 2 \
  --latency 1ms,5ms,20ms \
  --frames 200000 \
  --config configs/distributed_dual_sim.json
```

---

## 4. 측정 도구

### 4.1 Unity Profiler

**사용 목적**: Simulation Layer 성능 분석

**측정 항목**:
- Frame time (CPU/GPU)
- Rendering overhead
- Physics/Navigation update time
- Garbage Collection

**사용 방법**:
```
Unity Editor → Window → Analysis → Profiler
또는
Unity.Profiler API로 스크립트 삽입
```

### 4.2 BenchmarkDotNet (C#)

**사용 목적**: Pipeline Worker 성능 벤치마크

**예시**:
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60)]
public class PipelineBenchmark
{
    private RawImageData[] _testImages;

    [GlobalSetup]
    public void Setup()
    {
        _testImages = GenerateTestImages(100);
    }

    [Benchmark]
    public void AnnotationWorker_ProcessBatch()
    {
        var worker = new AnnotationWorker();
        foreach (var img in _testImages)
        {
            worker.ProcessJob(img);
        }
    }
}
```

**실행**:
```bash
dotnet run -c Release --project tests/performance/Benchmarks.csproj
```

### 4.3 Prometheus + Grafana

**사용 목적**: 실시간 런타임 모니터링

**메트릭 예시**:
```
# Prometheus metrics endpoint: /metrics

# FPS
forge_session_fps_current 14.5

# Queue depth
forge_pipeline_queue_depth{stage="capture"} 120
forge_pipeline_queue_depth{stage="detection"} 340

# Processing time
forge_worker_processing_time_ms{worker="detection"} 45.2

# Memory
forge_process_memory_bytes 4294967296
```

**Grafana Dashboard**:
- Panel 1: FPS (시계열 그래프)
- Panel 2: Queue Depth (각 Stage별 라인 차트)
- Panel 3: CPU/GPU 사용률 (게이지)
- Panel 4: 메모리 사용량 (시계열)

### 4.4 Custom Performance Logger

**사용 목적**: 세션별 상세 로그

```csharp
public class PerformanceLogger
{
    private Stopwatch _stopwatch;
    private int _processedFrames;

    public void LogFrame(int frameId, Dictionary<string, float> stageTimes)
    {
        _processedFrames++;
        var elapsed = _stopwatch.Elapsed.TotalSeconds;
        var fps = _processedFrames / elapsed;

        File.AppendAllText("perf.log",
            $"{frameId},{fps},{stageTimes["capture"]},{stageTimes["detection"]},...\n");
    }
}
```

**출력 예시** (`perf.log`):
```
frameId,fps,capture_ms,annotation_ms,tracking_ms,storage_ms
0,10.2,35.2,18.5,12.3,15.8
1,10.5,34.8,17.2,11.9,16.2
...
```

---

## 5. 성능 분석 방법

### 5.1 병목 지점 식별

**절차**:
1. Unity Profiler로 Simulation Layer 분석
   - Rendering > 50ms → GPU 병목
   - Physics/Navigation > 30ms → CPU 병목

2. Pipeline Stage별 처리 시간 측정
   ```
   Capture: 35ms
   Annotation: 8ms
   Tracking: 12ms
   Encode: 20ms
   Storage: 15ms
   ```

3. Queue depth 확인
   - Annotation Queue가 계속 증가 → AnnotationWorker 스레드/최적화 필요

**해결 방안**:
- GPU 병목: 해상도 낮추기, LOD 적용
- CPU 병목: Worker 병렬도 증가
- I/O 병목: SSD 사용, 버퍼 크기 증가

### 5.2 메모리 누수 탐지

**도구**: dotMemory, ANTS Memory Profiler

**절차**:
1. 세션 시작 시 메모리 스냅샷 (`mem_start`)
2. 10,000 프레임 생성 후 스냅샷 (`mem_mid`)
3. 세션 종료 후 스냅샷 (`mem_end`)

**분석**:
```
mem_start: 1.2GB
mem_mid:   2.5GB (예상 범위 내)
mem_end:   3.8GB (문제!)

→ 2.6GB 증가 (예상: <10% = 1.32GB)
→ 메모리 누수 의심
```

**원인 분석**:
- Retained objects 확인
- Event handler 미해제
- 대용량 객체 캐싱

### 5.3 프로파일링 결과 해석

**Unity Profiler 예시**:
```
CPU Time: 85ms
  - Rendering: 45ms (53%)
  - Scripts: 30ms (35%)
    - BehaviorSystem.Update: 18ms
    - CrowdService.Update: 10ms
  - Physics: 5ms (6%)
  - Other: 5ms (6%)

GPU Time: 60ms
  - Opaque Geometry: 40ms
  - Shadows: 15ms
  - Post-Processing: 5ms
```

**해석**:
- GPU가 병목 (60ms > 목표 50ms)
- Opaque Geometry 최적화 필요 (batching, culling)

---

## 6. 성능 최적화 전략

### 6.1 Simulation Layer

**렌더링**:
- GPU Instancing 활성화
- Occlusion Culling 적용
- LOD (Level of Detail) 사용
- Shadow distance 제한

**스크립트**:
- Update 대신 FixedUpdate 사용 (일정한 간격)
- Coroutine 대신 async/await
- Object Pooling (인물 생성/삭제)

### 6.2 Pipeline Layer

**Worker 병렬화**:
```csharp
// Before (순차)
foreach (var job in annotationJobs)
{
    ProjectToBbox(job);
}

// After (병렬)
Parallel.ForEach(annotationJobs,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    job => ProjectToBbox(job));
```

**벡터화/Span 활용**:
```csharp
// GT 투영 시 SIMD(System.Numerics) 사용
var personPositions = MemoryMarshal.Cast<float, Vector3>(personSpan);
for (int i = 0; i < personPositions.Length; i++)
{
    var projected = camera.Project(personPositions[i]);
    bboxes[i] = projected.ToBoundingBox();
}
```

**메모리 재사용**:
```csharp
// Buffer pooling
private static ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

var buffer = _bufferPool.Rent(imageSize);
try
{
    // Use buffer
}
finally
{
    _bufferPool.Return(buffer);
}
```

### 6.3 I/O 최적화

**비동기 쓰기**:
```csharp
await File.WriteAllBytesAsync(path, imageBytes);
```

**버퍼링**:
```csharp
using var stream = new BufferedStream(
    new FileStream(path, FileMode.Create),
    bufferSize: 1024 * 1024); // 1MB buffer
```

---

## 7. 회귀 테스트

### 7.1 Baseline 설정

Phase 1 완료 시 Baseline 측정:
```
Baseline (2023-11-01):
- FPS: 8.5
- Memory: 3.2GB
- CPU: 55%
- GPU: 68%
```

### 7.2 PR마다 성능 검증

```yaml
# .github/workflows/performance-check.yml
- name: Run performance benchmark
  run: dotnet run --project tests/performance

- name: Compare with baseline
  run: |
    python tools/compare_perf.py \
      --current perf_results.json \
      --baseline baseline_perf.json \
      --threshold 10  # 10% 이하 저하 허용
```

**임계값 초과 시**:
- PR 자동 차단
- 성능 저하 원인 분석 요구

---

## 8. 리포팅

### 8.1 성능 리포트 템플릿

```markdown
## Performance Test Results

**Environment**:
- OS: Ubuntu 20.04
- GPU: NVIDIA RTX 3080
- CPU: Intel i9-10900K
- RAM: 32GB

**Scenario**: Baseline (Phase 1)

**Results**:
| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| FPS | 5~10 | 8.5 | ✅ PASS |
| Memory | <4GB | 3.2GB | ✅ PASS |
| CPU | <60% | 55% | ✅ PASS |
| GPU | <70% | 68% | ✅ PASS |

**Bottleneck Analysis**:
- Annotation Worker: 12ms (정상)
- Recommendation: n/a

**Trend** (vs last week):
- FPS: +5% ↑
- Memory: -2% ↓
```

### 8.2 자동 리포트 생성

```python
# tools/generate_perf_report.py
def generate_report(manifest_path):
    with open(manifest_path) as f:
        data = json.load(f)

    fps = data['totalFrames'] / data['elapsedSeconds']
    memory = data['peakMemoryMB']

    report = f"""
    ## Performance Summary

    - FPS: {fps:.2f}
    - Memory: {memory:.0f} MB
    - Duration: {data['elapsedSeconds']/3600:.1f} hours
    """

    return report
```

---

## 9. Phase별 벤치마크 체크리스트

### Phase 1
- [ ] Baseline 시나리오 통과 (FPS 5~10)
- [ ] 메모리 사용 < 4GB
- [ ] 12시간 안정성 테스트 통과
- [ ] Baseline 리포트 작성

### Phase 2
- [ ] Multi-camera 시나리오 통과 (FPS 15~30)
- [ ] Worker 병렬화 효과 검증 (2x speedup)
- [ ] Queue back-pressure 테스트
- [ ] Scene 전환 오버헤드 < 5초

### Phase 3
- [ ] Large-scale 시나리오 통과 (1M frames, FPS 30~60)
- [ ] Stress 테스트 (최대 부하 측정)
- [ ] Multi-GPU 효과 검증 (선택)
- [ ] 최종 성능 리포트 작성

---

## 10. 측정 데이터 관리 절차

Phase별 목표 대비 실제 측정치를 추적하기 위해 다음 절차를 표준화한다.

1. **결과 수집**
   - 세션 종료 후 `meta/manifest.json`과 Prometheus(또는 BenchmarkDotNet) raw 결과를 `pipeline/validation/perf_logs/<YYYYMMDD>_<scenario>.json`으로 복사한다.
   - `tools/generate_perf_report.py`를 실행해 Markdown 요약을 생성하고 같은 디렉터리에 저장한다.
2. **카탈로그 업데이트**
   - `pipeline/validation/perf_logs/index.csv`에 Run ID, Scenario, Commit SHA, 하드웨어 스펙, FPS/메모리/큐 지표, Pass/Fail 여부를 append한다.
   - Run ID는 `PERF-<date>-<seq>` 형식으로 관리한다.
3. **리뷰 & 회귀 감시**
   - PR/릴리스 시 최신 Pass 기록과 목표치 비교 그래프를 README 또는 Dashboard에 첨부한다.
   - Threshold 초과 시 자동 알림(예: GitHub Status, Slack)을 트리거하여 Regression을 조기 감지한다.
4. **보관 정책**
   - 원본 메트릭 파일은 1년 보관, 요약 리포트는 무기한 보관한다.
   - 기밀 하드웨어 정보(IP/호스트명)는 `Security_and_Compliance.md` §2.4 규칙에 따라 마스킹한다.

해당 절차는 QA/Perf 엔지니어의 공용 체크리스트로 간주하며, 위 경로 구조가 생성돼 있지 않다면 초기화 스크립트를 통해 빈 카탈로그를 만들어야 한다.

---

## 11. 참고 자료

- [Unity Profiler Manual](https://docs.unity3d.com/Manual/Profiler.html)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/)
- [.NET Performance Tips](https://docs.microsoft.com/en-us/dotnet/core/performance/)
