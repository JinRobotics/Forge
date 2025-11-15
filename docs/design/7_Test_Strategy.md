# Forge - Test Strategy

> **문서 버전:** v1.0 (2025-02-14)  
> **변경 이력:**  
> - v1.0 (2025-02-14): 초기 작성

## 1. 목적

본 문서는 Forge의 테스트 전략을 정의한다.

- 품질 목표 설정
- 테스트 레벨 정의 (단위/통합/E2E/성능)
- Phase별 테스트 범위
- CI/CD 통합 전략

---

## 2. 품질 목표

### 2.1 코드 품질
- **코드 커버리지**: 최소 70% (Phase 1), 80% (Phase 2+)
- **Critical Path 커버리지**: 100% (GenerationController, Pipeline Workers)
- **정적 분석**: 0 Critical Issues (SonarQube/ReSharper)

### 2.2 기능 품질
- **자동 테스트 통과율**: 100%
- **회귀 테스트**: 매 PR마다 실행, 실패 시 merge 차단
- **수동 QA**: Phase 종료 시 1회 수행

### 2.3 성능 품질
- **FPS 목표 달성**: NFR-01 기준 충족
- **메모리 누수**: 12시간 실행 시 메모리 증가 < 10%
- **디스크 I/O**: 손상률 < 0.01%

---

## 3. 테스트 레벨

### 3.1 단위 테스트 (Unit Tests)

**목적**: 개별 클래스/메서드의 로직 검증

**범위**:
- 모든 public 메서드
- Edge case, boundary condition
- 에러 처리 로직

**도구**:
- xUnit / NUnit
- Moq (mocking framework)
- FluentAssertions

**예시**:
```csharp
// tests/unit/Orchestration/SessionManagerTests.cs
[Fact]
public void CreateSession_ValidConfig_ReturnsSessionContext()
{
    // Arrange
    var config = new SessionConfig
    {
        SessionId = "test_session",
        TotalFrames = 1000,
        // ...
    };
    var sessionManager = new SessionManager();

    // Act
    var context = sessionManager.CreateSession(config);

    // Assert
    context.Should().NotBeNull();
    context.Config.SessionId.Should().Be("test_session");
    Directory.Exists(context.SessionDirectory).Should().BeTrue();
}

[Fact]
public void CreateSession_DuplicateSessionId_ThrowsException()
{
    // Arrange
    var config = new SessionConfig { SessionId = "duplicate" };
    var sessionManager = new SessionManager();
    sessionManager.CreateSession(config);

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() =>
        sessionManager.CreateSession(config));
}
```

**Phase 1 필수 테스트**:
- [ ] ConfigurationLoader: 유효/무효 config 검증
- [ ] SessionManager: Create/Start/Stop
- [ ] FrameContext: ID 생성 로직
- [ ] CaptureWorker: 이미지 캡처 모의 테스트
- [ ] DetectionWorker: bbox 생성 로직
- [ ] TrackingWorker: track ID 할당
- [ ] StorageWorker: 파일 쓰기

### 3.2 통합 테스트 (Integration Tests)

**목적**: 여러 컴포넌트 간 상호작용 검증

**범위**:
- Orchestration Layer ↔ Simulation Layer
- Pipeline Stage 간 데이터 전달
- FrameBus ↔ Workers
- SessionManager ↔ ValidationService

**도구**:
- xUnit / NUnit
- Testcontainers (필요 시)
- In-memory 파일 시스템

**예시**:
```csharp
// tests/integration/PipelineFlowTests.cs
[Fact]
public async Task Pipeline_FullFlow_GeneratesValidOutput()
{
    // Arrange
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    var config = new SessionConfig
    {
        SessionId = "integration_test",
        TotalFrames = 100,
        OutputDirectory = tempDir,
        // ...
    };

    var sessionManager = new SessionManager();
    var pipelineCoordinator = new PipelineCoordinator();
    // ... setup workers

    // Act
    var session = sessionManager.CreateSession(config);
    await sessionManager.StartAsync(session);

    // Wait for completion
    await Task.Delay(10000); // or use event

    // Assert
    var outputImages = Directory.GetFiles(
        Path.Combine(tempDir, "images", "cam01"),
        "*.jpg"
    );
    outputImages.Should().HaveCountGreaterThan(90); // allow some frame loss

    var manifestPath = Path.Combine(tempDir, "meta", "manifest.json");
    File.Exists(manifestPath).Should().BeTrue();

    // Cleanup
    Directory.Delete(tempDir, true);
}
```

**Phase 1 필수 테스트**:
- [ ] FrameBus → CaptureWorker → DetectionWorker 흐름
- [ ] Session 전체 라이프사이클 (Init → Run → Stop)
- [ ] Scene 전환 시 카메라 재설정
- [ ] Validation/Stats/Manifest 생성

### 3.3 End-to-End 테스트 (E2E Tests)

**목적**: 실제 사용자 시나리오 검증

**범위**:
- CLI 명령 실행
- 전체 세션 실행 (소규모: 1,000 프레임)
- 출력 데이터 품질 검증
- 에러 발생 시 복구

**도구**:
- Bash 스크립트 또는 Python
- Unity Headless 모드
- 실제 파일 시스템

**예시**:
```bash
# tests/e2e/test_basic_session.sh
#!/bin/bash

set -e

# Cleanup
rm -rf /tmp/e2e_output

# Run session
dotnet run --project src/Application -- \
  --config tests/e2e/fixtures/basic_session.json

# Verify output
if [ ! -d "/tmp/e2e_output/session_e2e_basic/images" ]; then
  echo "ERROR: Images directory not found"
  exit 1
fi

IMAGE_COUNT=$(find /tmp/e2e_output/session_e2e_basic/images -name "*.jpg" | wc -l)
if [ "$IMAGE_COUNT" -lt 900 ]; then
  echo "ERROR: Expected at least 900 images, found $IMAGE_COUNT"
  exit 1
fi

# Verify manifest
if [ ! -f "/tmp/e2e_output/session_e2e_basic/meta/manifest.json" ]; then
  echo "ERROR: Manifest not found"
  exit 1
fi

echo "E2E test passed!"
```

**Phase 1 필수 테스트**:
- [ ] 기본 세션 (Factory, 3 cameras, 1,000 frames)
- [ ] Config 검증 실패 시나리오
- [ ] 디스크 공간 부족 시나리오
- [ ] 중간 중단 후 재시작 (Phase 2)

### 3.4 성능 테스트 (Performance Tests)

**목적**: NFR 목표 달성 여부 검증

**범위**:
- FPS 벤치마크
- 메모리 사용량
- 디스크 I/O 처리량
- Queue depth 모니터링

**도구**:
- BenchmarkDotNet (C# 성능 측정)
- Unity Profiler
- Prometheus + Grafana (런타임 모니터링)

**예시**:
```csharp
// tests/performance/FpsBenchmark.cs
[MemoryDiagnoser]
public class FpsBenchmark
{
    private SessionConfig _config;
    private SessionManager _sessionManager;

    [GlobalSetup]
    public void Setup()
    {
        _config = new SessionConfig
        {
            SessionId = "perf_test",
            TotalFrames = 10000,
            // ...
        };
        _sessionManager = new SessionManager();
    }

    [Benchmark]
    public void GenerateFrames_10k()
    {
        var session = _sessionManager.CreateSession(_config);
        _sessionManager.Start(session);
        // Wait for completion
    }
}
```

**Phase별 성능 목표**:
- Phase 1: 5~10 FPS @ 3 cameras, 30 people
- Phase 2: 15~30 FPS @ 6 cameras, 80 people
- Phase 3: 30~60 FPS @ 6 cameras, 100 people

**Phase 1 필수 테스트**:
- [ ] Baseline FPS 측정 (Factory, 3 cameras)
- [ ] 메모리 누수 테스트 (1시간 실행)
- [ ] Queue back-pressure 테스트

---

## 4. 테스트 디렉토리 구조

```
tests/
├── unit/                       # 단위 테스트
│   ├── Application/
│   │   ├── ConfigurationLoaderTests.cs
│   │   └── ProgressReporterTests.cs
│   ├── Orchestration/
│   │   ├── SessionManagerTests.cs
│   │   ├── GenerationControllerTests.cs
│   │   └── PipelineCoordinatorTests.cs
│   ├── DataPipeline/
│   │   ├── CaptureWorkerTests.cs
│   │   ├── DetectionWorkerTests.cs
│   │   ├── TrackingWorkerTests.cs
│   │   └── StorageWorkerTests.cs
│   └── Services/
│       ├── ValidationServiceTests.cs
│       └── StatsServiceTests.cs
│
├── integration/                # 통합 테스트
│   ├── PipelineFlowTests.cs
│   ├── SessionLifecycleTests.cs
│   └── SceneTransitionTests.cs
│
├── e2e/                        # End-to-End 테스트
│   ├── test_basic_session.sh
│   ├── test_multiscene.sh
│   ├── test_error_recovery.sh
│   └── fixtures/
│       ├── basic_session.json
│       └── multiscene.json
│
├── performance/                # 성능 테스트
│   ├── FpsBenchmark.cs
│   ├── MemoryLeakTests.cs
│   └── LoadTests.cs
│
└── fixtures/                   # 테스트 데이터
    ├── configs/
    ├── images/
    └── labels/
```

---

## 5. CI/CD 통합

### 5.1 PR Workflow

```yaml
# .github/workflows/pr.yml
name: Pull Request Checks

on: [pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '6.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Run unit tests
        run: dotnet test tests/unit --no-build --verbosity normal
      - name: Code coverage
        run: |
          dotnet test tests/unit --collect:"XPlat Code Coverage"
          # Upload to Codecov

  integration-tests:
    runs-on: ubuntu-latest
    needs: unit-tests
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
      - name: Run integration tests
        run: dotnet test tests/integration --verbosity normal

  static-analysis:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Run SonarQube scan
        # ... SonarQube 설정
```

### 5.2 Nightly Build

```yaml
# .github/workflows/nightly.yml
name: Nightly Performance Tests

on:
  schedule:
    - cron: '0 2 * * *'  # 매일 새벽 2시

jobs:
  performance-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Run performance benchmarks
        run: dotnet run --project tests/performance
      - name: Upload results
        # Grafana/Prometheus에 결과 전송

  long-running-test:
    runs-on: ubuntu-latest
    timeout-minutes: 720  # 12 hours
    steps:
      - name: Run 12-hour stability test
        run: bash tests/e2e/test_long_running.sh
```

---

## 6. 테스트 데이터 관리

### 6.1 Fixture 관리
- **Golden Files**: 예상 출력 데이터 (이미지, 라벨)
- **Config Templates**: 재사용 가능한 설정 파일
- **Mock Data**: Simulation 대체용 합성 데이터

### 6.2 데이터 크기 제한
- Unit/Integration: 최대 10MB
- E2E: 최대 100MB
- Performance: 최대 1GB

---

## 7. Phase별 테스트 계획

### Phase 1 (MVP)
**목표**: 기본 기능 검증

- [ ] 단위 테스트: 50개 이상
- [ ] 통합 테스트: 10개 이상
- [ ] E2E 테스트: 3개 이상
- [ ] 성능 테스트: Baseline FPS 측정
- [ ] 코드 커버리지: > 70%

### Phase 2 (확장)
**목표**: 안정성 및 성능 검증

- [ ] 단위 테스트: 100개 이상
- [ ] 통합 테스트: 20개 이상
- [ ] E2E 테스트: 10개 이상
- [ ] 성능 테스트: 목표 FPS 15~30 달성
- [ ] 코드 커버리지: > 80%
- [ ] 12시간 안정성 테스트 통과

### Phase 3 (최적화)
**목표**: 대규모 데이터셋 품질 검증

- [ ] Load Testing: 1M 프레임 생성
- [ ] Stress Testing: 최대 부하 테스트
- [ ] Dataset Quality Validation: Diversity/Balance/Realism Score
- [ ] 분산 환경 테스트 (Multi-GPU, Multi-machine)

---

## 8. 테스트 Best Practices

### 8.1 AAA 패턴 (Arrange-Act-Assert)
```csharp
[Fact]
public void Example_Test()
{
    // Arrange: 테스트 준비
    var input = new Input();

    // Act: 테스트 실행
    var result = SystemUnderTest.Process(input);

    // Assert: 결과 검증
    result.Should().NotBeNull();
}
```

### 8.2 테스트 격리
- 각 테스트는 독립적으로 실행 가능해야 함
- 공유 상태 사용 금지
- Cleanup 철저히 (파일, 메모리)

### 8.3 의미 있는 테스트 이름
```csharp
// ❌ Bad
[Fact] public void Test1() { }

// ✅ Good
[Fact] public void CreateSession_DuplicateId_ThrowsException() { }
```

### 8.4 빠른 실행
- 단위 테스트: < 100ms per test
- 통합 테스트: < 5s per test
- E2E 테스트: < 5min per test

---

## 9. 테스트 메트릭 및 리포팅

### 9.1 추적 메트릭
- 테스트 통과율 (Pass Rate)
- 코드 커버리지 (Code Coverage)
- 테스트 실행 시간 (Execution Time)
- 실패 테스트 트렌드 (Failure Trend)

### 9.2 리포팅
- PR마다 자동 코멘트 (테스트 결과 요약)
- 주간 리포트 (통과율, 커버리지, 실패 원인)
- Dashboard (Grafana): 실시간 메트릭 시각화

---

## 10. 참고 자료

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [FluentAssertions](https://fluentassertions.com/)
