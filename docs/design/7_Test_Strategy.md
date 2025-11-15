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
- [ ] config 저장 시 민감 필드(API Key/토큰/사용자 경로) 필터링/마스킹 확인
- [ ] `/status` 계약/인증 테스트 (HTTP 게이트웨이 모드 활성 시): 인증 누락 시 401, `queueDepthSummary` 존재, 내부 경로/사용자명 미포함
- [ ] `IFrameRatePolicy` 계약 테스트: BackPressureLevel별 Decision(Generate/Skip/Pause) 매핑, 정책 선택 config 반영 여부
- [ ] `ISimulationGateway` 계약 테스트: Initialize/GenerateFrame/GetActiveCameras가 Mock/Unity/HTTP 구현에서 동일 시그니처로 동작하는지
- [ ] 이동형 카메라 pose 직렬화/전파: FrameContext → FramePipelineContext → ManifestWriter까지 pose 정보가 손실되지 않는지 확인

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
- [ ] Checkpoint 저장/복구: lastStoredFrame 기준 재개, 전 카메라/라벨 누락 검증 경고 확인 (Phase 2+ 옵션. Phase 1은 수동/비필수)
- [ ] 이동형 카메라 세션:
  - pose 파일 생성(`camera_poses/`), manifest에 poseFile 경로/유형(static/mobile) 표시
  - pose CSV row 수 = frame 수 ±1% 이내 확인
  - waypoint 대비 위치 편차 ≤ 0.15m, 회전 편차 ≤ 3° 검증
  - timestamp 단조 증가 및 샘플링 주기(설정값 ±10%) 확인
  - Validation 리포트에 `poseMissingCount`, `poseDriftWarning` 필드 노출

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

### Phase 4 (Robotics Extension)
**목표**: Unity + Isaac 동기화 및 센서 Export 품질 보증

- [ ] MultiSimSyncCoordinator/IRoboticsGateway 계약 테스트: Frame-aligned step, syncPolicy(maxDelayMs/timeoutMs/onTimeout) 준수
- [ ] 센서 Ground Truth 통합 테스트: robot_pose/lidar/imu/wheel_odom/depth 디렉터리 구조, manifest.sensorArtifacts checksum 검증
- [ ] SLAM Export 회귀 테스트: TUM/KITTI 파일 수 = frame 수 ±1%, timestamp 단조 증가, trajectory drift < 1% (허용 오차)
- [ ] Sensor Quality Validation: 누락/드리프트/포맷 오류 시 ValidationService가 `sensorMissingCount`, `sensorDriftWarning`을 기록하는지 확인
- [ ] 장애 복원력 테스트: Isaac 지연/오류 주입 시 strict 모드 FAIL, relaxed 모드 drop+카운터 증가 여부 확인

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

## 10. Unity 의존성 Mock 전략

### 10.1 문제점

Unity 의존성으로 인한 테스트 어려움:
- Unity 타입 (Vector3, Quaternion 등)은 Unity 런타임 없이 인스턴스화 불가
- MonoBehaviour는 `new` 연산자로 생성 불가
- Unity Editor에서만 실행 가능한 코드가 많음

### 10.2 해결 전략

#### **전략 1: Adapter 패턴 (권장)**

Unity 타입을 직접 사용하지 않고, 인터페이스로 추상화

**예시: ISimulationGateway**
```csharp
// Orchestration Layer (Unity 독립적)
public interface ISimulationGateway
{
    Task InitializeAsync(SessionConfig config);
    Task<FrameContext> GenerateFrameAsync(int frameId);
    void Shutdown();
}

// Simulation Layer (Unity 의존)
public class InProcessSimulationGateway : MonoBehaviour, ISimulationGateway
{
    private readonly CrowdService _crowdService;  // Unity MonoBehaviour

    public async Task<FrameContext> GenerateFrameAsync(int frameId)
    {
        // Unity API 호출
        var personStates = _crowdService.GetAllPersonStates();
        return new FrameContext { FrameId = frameId, PersonStates = personStates };
    }
}

// 테스트용 Mock
public class MockSimulationGateway : ISimulationGateway
{
    public Task<FrameContext> GenerateFrameAsync(int frameId)
    {
        // Unity 없이 테스트 데이터 반환
        return Task.FromResult(new FrameContext
        {
            FrameId = frameId,
            PersonStates = new List<PersonState>
            {
                new() { GlobalPersonId = 1, Position = new float[] {0, 0, 0} }
            }
        });
    }
}
```

**테스트 예시**:
```csharp
[Fact]
public async Task GenerationController_GenerateFrame_CallsSimulationGateway()
{
    // Arrange
    var mockGateway = new MockSimulationGateway();
    var controller = new GenerationController(mockGateway);

    // Act
    await controller.GenerateFrameAsync(0);

    // Assert
    // Unity 없이 순수 C# 로직만 테스트
}
```

---

#### **전략 2: Unity Test Framework (통합 테스트용)**

Unity Editor 내에서 실행되는 테스트

**설치**:
```bash
# Unity Package Manager에서 설치
com.unity.test-framework
```

**예시**:
```csharp
// tests/unity/CrowdServiceTests.cs (Unity Editor에서 실행)
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class CrowdServiceTests
{
    [UnityTest]
    public IEnumerator CrowdService_SpawnPerson_CreatesGameObject()
    {
        // Arrange
        var crowdService = new GameObject().AddComponent<CrowdService>();

        // Act
        crowdService.SpawnPerson(globalPersonId: 1);
        yield return null;  // 1 프레임 대기

        // Assert
        var agents = Object.FindObjectsOfType<PersonAgent>();
        Assert.AreEqual(1, agents.Length);
    }
}
```

**실행 방법**:
```bash
# Unity Editor: Window > General > Test Runner
# 또는 CLI:
unity-editor -runTests -testPlatform PlayMode -testResults results.xml
```

---

#### **전략 3: 데이터 구조 Unity 독립성 보장**

**원칙**: 데이터 모델은 Unity 타입 사용 금지

❌ **Bad**:
```csharp
public class PersonState
{
    public Vector3 Position { get; set; }  // Unity 타입!
    public Quaternion Rotation { get; set; }
}
```

✅ **Good**:
```csharp
public class PersonState
{
    public float[] Position { get; set; }  // [x, y, z]
    public float[] Rotation { get; set; }  // [pitch, yaw, roll]
}
```

**이점**:
- Unity 없이 데이터 모델 테스트 가능
- JSON 직렬화 문제 없음
- Orchestration Layer 완전 독립

---

#### **전략 4: Mock 프레임워크 활용 (Moq)**

인터페이스 기반 의존성은 Moq로 Mock

**예시**:
```csharp
[Fact]
public async Task PipelineCoordinator_CheckBackPressure_SlowsGeneration()
{
    // Arrange
    var mockGateway = new Mock<ISimulationGateway>();
    mockGateway
        .Setup(x => x.GenerateFrameAsync(It.IsAny<int>()))
        .ReturnsAsync(new FrameContext { FrameId = 0 });

    var controller = new GenerationController(mockGateway.Object);

    // Act
    await controller.GenerateFrameAsync(0);

    // Assert
    mockGateway.Verify(x => x.GenerateFrameAsync(0), Times.Once);
}
```

---

### 10.3 테스트 계층 분리

| 계층 | 테스트 방법 | 실행 환경 | 예시 |
|------|------------|----------|------|
| **Orchestration Layer** | 단위 테스트 (Moq) | CLI (.NET) | GenerationController, PipelineCoordinator |
| **DataPipeline** | 단위 테스트 (실제 데이터) | CLI (.NET) | CaptureWorker, DetectionWorker |
| **Data Models** | 단위 테스트 (순수 C#) | CLI (.NET) | FrameContext, TrackingData |
| **Simulation Layer** | Unity Test Framework | Unity Editor | CrowdService, EnvironmentCoordinator |
| **통합** | E2E (Headless Unity) | CLI + Unity | 전체 파이프라인 |

---

### 10.4 Headless Unity 실행 (E2E 테스트)

Unity를 CLI에서 실행하여 E2E 테스트

**실행 스크립트**:
```bash
#!/bin/bash
# tests/e2e/run_headless_unity.sh

unity-editor \
  -batchmode \
  -nographics \
  -silent-crashes \
  -logFile unity.log \
  -executeMethod ForgeTestRunner.RunE2ETest \
  -testConfig tests/e2e/fixtures/basic_session.json

# Unity 종료 코드 확인
if [ $? -ne 0 ]; then
  echo "Unity E2E test failed"
  cat unity.log
  exit 1
fi
```

**Unity 테스트 진입점**:
```csharp
// ForgeTestRunner.cs (Unity 프로젝트 내)
public static class ForgeTestRunner
{
    public static void RunE2ETest()
    {
        var configPath = GetCommandLineArg("-testConfig");
        var config = LoadConfig(configPath);

        // 세션 실행
        var session = SessionManager.CreateSession(config);
        SessionManager.Start(session);

        // 완료 대기
        while (!session.IsCompleted)
        {
            Thread.Sleep(100);
        }

        // 검증
        ValidateOutput(session);

        // 종료 코드 설정
        EditorApplication.Exit(session.HasErrors ? 1 : 0);
    }
}
```

---

### 10.5 CI/CD 통합 (Unity 테스트)

**GitHub Actions 예시**:
```yaml
# .github/workflows/unity-tests.yml
name: Unity Integration Tests

on: [pull_request]

jobs:
  unity-tests:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v2

      - name: Cache Unity Library
        uses: actions/cache@v2
        with:
          path: Library
          key: Library-${{ hashFiles('ProjectSettings/ProjectVersion.txt') }}

      - name: Run Unity Tests
        uses: game-ci/unity-test-runner@v2
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          testMode: PlayMode

      - name: Upload Test Results
        uses: actions/upload-artifact@v2
        if: always()
        with:
          name: Test results
          path: artifacts
```

---

### 10.6 Mock 데이터 생성 도구

**FixtureBuilder 클래스**:
```csharp
// tests/fixtures/FixtureBuilder.cs
public static class FixtureBuilder
{
    public static FrameContext CreateFrameContext(int frameId = 0, int personCount = 10)
    {
        return new FrameContext
        {
            FrameId = frameId,
            Timestamp = DateTime.UtcNow,
            SceneName = "Factory",
            PersonStates = Enumerable.Range(1, personCount)
                .Select(id => new PersonState
                {
                    GlobalPersonId = id,
                    Position = new[] { Random.Shared.NextSingle() * 10, 0, Random.Shared.NextSingle() * 10 },
                    Velocity = new[] { 1.0f, 0, 0 }
                })
                .ToList()
        };
    }

    public static RawImageData CreateMockImage(int width = 1920, int height = 1080)
    {
        var pixels = new byte[width * height * 3];
        Random.Shared.NextBytes(pixels);

        return new RawImageData
        {
            Width = width,
            Height = height,
            PixelData = pixels,
            Format = ImageFormat.RGB24
        };
    }
}
```

**사용 예시**:
```csharp
[Fact]
public void DetectionWorker_ProcessFrame_ReturnsDetections()
{
    // Arrange
    var mockImage = FixtureBuilder.CreateMockImage();
    var worker = new DetectionWorker();

    // Act
    var detections = worker.ProcessFrame(mockImage);

    // Assert
    detections.Should().NotBeEmpty();
}
```

---

### 10.7 테스트 디렉토리 구조 (최종)

```
tests/
├── unit/                       # Unity 독립 단위 테스트 (.NET CLI)
│   ├── Orchestration/
│   ├── DataPipeline/
│   └── DataModels/
│
├── unity/                      # Unity Test Framework (Unity Editor)
│   ├── Simulation/
│   │   ├── CrowdServiceTests.cs
│   │   └── EnvironmentCoordinatorTests.cs
│   └── Integration/
│       └── FrameCaptureTests.cs
│
├── integration/                # 통합 테스트 (Mock Gateway 사용)
│   ├── PipelineFlowTests.cs
│   └── SessionLifecycleTests.cs
│
├── e2e/                        # E2E (Headless Unity)
│   ├── run_headless_unity.sh
│   └── fixtures/
│
├── fixtures/                   # 테스트 데이터
│   ├── FixtureBuilder.cs       # Mock 데이터 생성기
│   └── configs/
│
└── mocks/                      # Mock 구현
    ├── MockSimulationGateway.cs
    └── MockDetectionService.cs
```

---

### 10.8 Phase 1 필수 Mock 구현

- [ ] `MockSimulationGateway`: Unity 없이 FrameContext 생성
- [ ] `MockDetectionService`: 고정된 bbox 반환
- [ ] `FixtureBuilder`: 테스트 데이터 생성기
- [ ] `InMemoryFileSystem`: 디스크 I/O 없는 StorageWorker 테스트

---

## 11. 참고 자료

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [FluentAssertions](https://fluentassertions.com/)
- [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest)
- [GameCI (Unity CI/CD)](https://game.ci/)
