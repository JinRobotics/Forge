
## 1. 목적

Forge의 Unity Simulation Layer를 C# Orchestration Layer와 통합하는 방법을 상세히 정의한다.

**핵심 질문**:
- Unity를 어떻게 실행하는가? (Playmode/Standalone/Headless)
- InProcess 모드에서 C#과 Unity가 어떻게 통신하는가?
- Remote 모드에서 Unity를 어떻게 빌드/실행하는가?

---

## 2. Unity 실행 모드 개요

### 2.1 실행 모드 비교

| 모드 | Unity 실행 방식 | 통신 방법 | Phase | 용도 |
|------|----------------|-----------|-------|------|
| **InProcess** | Unity Editor Playmode | MonoBehaviour 직접 호출 | Phase 1 | 개발/디버깅 |
| **Remote** | Unity Standalone Build | HTTP REST API | Phase 2 | 프로덕션 |
| **Headless** | Unity Headless Build | HTTP REST API | Phase 3 | 서버/클라우드 |
| **Distributed** | 다수 Unity Worker | HTTP + Master 조율 | Phase 3 | 대규모 생성 |

---

## 3. InProcess 모드 (Phase 1)

### 3.1 개념

C# Orchestration 코드가 **Unity Editor 내부에서** MonoBehaviour로 실행된다.

```
Unity Editor (Single Process)
├── Simulation Layer (MonoBehaviour)
│   ├── InProcessSimulationGateway
│   ├── CameraService
│   ├── CrowdService
│   └── EnvironmentService
└── Orchestration Layer (C# Runtime)
    ├── SessionManager
    ├── GenerationController
    └── PipelineCoordinator
```

### 3.2 실행 방법

#### 옵션 1: Unity Editor Playmode (권장)

```bash
# 1. Unity Editor 실행
unity-editor -projectPath /path/to/forge/unity

# 2. Playmode 진입 (Play 버튼 클릭)

# 3. AppEntrypoint.cs가 자동 실행됨
```

**AppEntrypoint.cs** (Unity Scene에 배치):
```csharp
using UnityEngine;

public class AppEntrypoint : MonoBehaviour
{
    private SessionManager _sessionManager;
    private GenerationController _generationController;

    void Start()
    {
        // Config 로드
        var config = LoadConfig("session_config.json");

        // InProcess Gateway 초기화
        var gateway = gameObject.AddComponent<InProcessSimulationGateway>();

        // Orchestration 시작
        _sessionManager = new SessionManager(config, gateway);
        _generationController = new GenerationController(_sessionManager);

        // Frame 생성 시작
        _generationController.Start();
    }

    void Update()
    {
        // 매 프레임 GenerationController 실행
        _generationController.Update(Time.deltaTime);
    }
}
```

**장점**:
- ✅ 실시간 디버깅 가능 (Inspector, Scene View)
- ✅ 코드 수정 후 즉시 테스트
- ✅ Unity Profiler 사용 가능

**단점**:
- ❌ GUI 렌더링 오버헤드
- ❌ Editor-only 기능 의존 위험

---

#### 옵션 2: Unity Standalone Build (고급)

```bash
# 1. Unity Standalone Build
unity-editor \
  -quit \
  -batchmode \
  -buildTarget StandaloneLinux64 \
  -executeMethod Forge.BuildScripts.BuildInProcessMode

# 2. Standalone 실행
./forge-unity-standalone --config session_config.json
```

**BuildInProcessMode.cs**:
```csharp
public static class BuildScripts
{
    [MenuItem("Forge/Build InProcess Standalone")]
    public static void BuildInProcessMode()
    {
        var scenes = new[] { "Assets/Scenes/MainScene.unity" };

        BuildPipeline.BuildPlayer(
            scenes,
            "Builds/forge-unity-standalone",
            BuildTarget.StandaloneLinux64,
            BuildOptions.None
        );
    }
}
```

**장점**:
- ✅ Editor 오버헤드 없음
- ✅ 프로덕션 빌드 테스트

**단점**:
- ❌ 디버깅 어려움
- ❌ 빌드 시간 소요

---

### 3.3 InProcessSimulationGateway 구현

```csharp
public class InProcessSimulationGateway : MonoBehaviour, ISimulationGateway
{
    private CameraService _cameraService;
    private CrowdService _crowdService;
    private EnvironmentService _environmentService;

    void Awake()
    {
        // Unity Scene에서 서비스 찾기
        _cameraService = FindObjectOfType<CameraService>();
        _crowdService = FindObjectOfType<CrowdService>();
        _environmentService = FindObjectOfType<EnvironmentService>();
    }

    public async Task InitializeAsync(SessionConfig config)
    {
        // Unity Main Thread에서 실행
        await _environmentService.LoadSceneAsync(config.Scenes[0].Name);
        _cameraService.InitializeCameras(config.Cameras);
        _crowdService.SpawnInitialCrowd(config.Crowd);
    }

    public async Task<FrameContext> GenerateFrameAsync(int frameId)
    {
        // Unity Update 사이클에서 실행
        var personStates = _crowdService.GetPersonStates();
        var cameraPoses = _cameraService.GetCameraPoses();

        return new FrameContext
        {
            FrameId = frameId,
            Timestamp = Time.time,
            PersonStates = personStates,
            CameraPoses = cameraPoses
        };
    }

    public List<CameraMeta> GetActiveCameras()
    {
        return _cameraService.GetActiveCameraMetadata();
    }

    public void Shutdown()
    {
        // 정리
        _crowdService.DespawnAll();
    }
}
```

**Thread Safety 주의**:
```csharp
// ❌ 잘못된 예 (Worker Thread에서 Unity API 호출)
Task.Run(() => Camera.main.Render()); // Exception!

// ✅ 올바른 예 (Main Thread에서 실행)
public async Task<byte[]> CaptureImageAsync()
{
    // Main Thread Dispatcher 사용
    return await UnityMainThreadDispatcher.RunOnMainThread(() =>
    {
        Camera.main.Render();
        return ReadPixels();
    });
}
```

---

### 3.4 Config 예시

```json
{
  "sessionId": "inprocess_test_001",
  "totalFrames": 10000,
  "simulationGateway": { "mode": "inprocess" },
  "scenes": [
    { "name": "Factory", "durationFrames": 10000 }
  ],
  "cameras": [
    {
      "id": "cam01",
      "type": "static",
      "position": [0, 2, -5],
      "rotation": [0, 0, 0],
      "resolution": "1920x1080"
    }
  ],
  "crowd": { "minCount": 20, "maxCount": 30 },
  "timeWeather": { "timeOfDay": "day", "brightness": 1.0 },
  "output": { "imageFormat": "jpg", "labelFormats": ["json"] }
}
```

---

### 3.5 Unity 패키지 구조

| 경로 | 설명 | 비고 |
|------|------|------|
| `Packages/com.forge.simulation/Runtime` | Simulation Layer 런타임 코드 (`CameraService`, `CrowdService`, `TimeWeatherService`) | `asmdef`: `Forge.Simulation.Runtime` |
| `Packages/com.forge.simulation/Editor` | Validator, SceneAsset 인입 툴, Build 스크립트 | `asmdef`: `Forge.Simulation.Editor`, Editor 전용 의존성만 허용 |
| `Assets/Scenes/Runtime/` | 기본 Scene, Diagnostics Scene | Addressables로 관리 |
| `Assets/Forge/Prefabs/` | 카메라 리그, Crowd Agent, Debug UI 프리팹 | Prefab Variant 사용 |
| `Assets/Forge/ScriptableObjects/` | Randomization preset, Lighting profile | `ISceneMetadataProvider`가 참조 |

**규칙**
- 모든 런타임 스크립트는 `Runtime` 폴더에 위치시키고 Editor 의존성을 제거한다.
- 패키지는 UPM으로 배포하며, Orchestration 레이어는 `Packages/manifest.json`를 통해 버전을 고정한다.
- `Packages/Forge/Runtime/Bridge` 폴더에 `ISimulationGateway` 구현을 두고, HTTP/InProcess 모드를 `#if UNITY_EDITOR` 전처리로 분리하지 않는다(동일 어셈블리 내에서 Strategy 패턴 사용).

---

### 3.6 MonoBehaviour 라이프사이클 ↔ 파이프라인 연동

| MonoBehaviour 훅 | Forge 역할 | 호출 순서 |
|-----------------|-----------|-----------|
| `Awake` | Service Locator 초기화, Camera/Crowd Prefab 로드 | EnvironmentService → CameraService → CrowdService |
| `Start` | SessionConfig ingest, Scene prewarm, FrameBus 연결 | `SimulationBootstrapper` 실행 |
| `Update` | Crowd 행동 업데이트, FrameContext 작성 | `CrowdSystem.UpdateAgents()` → `FrameContextBuilder.Snapshot()` |
| `LateUpdate` | 카메라 포즈 적용, CaptureBridge 호출 | `CameraService.ApplyPoses()` → `CaptureBridge.RequestCapture()` |
| `OnDestroy` | Worker unsubscribe, Buffer 반환 | PipelineCoordinator.Dispose 호출 |

**지침**
- Orchestration에서 전달된 `FrameTick`은 `Update` 후반부에서 처리하고, Capture는 `LateUpdate`에서 수행해 motion blur/rolling shutter가 반영되도록 한다.
- Remote 모드에서도 동일 순서를 강제하기 위해 `TimelineIntegrator`가 Unity PlayerLoop를 커스터마이징하고, Isaac/로보틱스가 활성화된 경우에는 해당 순서에 `MultiSimSyncCoordinator`를 삽입한다.

---

### 3.7 Editor/Inspector 확장

- **SessionConfig Inspector**: `ForgeSessionConfigEditor`가 JSON Config를 ScriptableObject로 로드/저장하며 필드별 유효성 검사를 수행한다. Scene View에서 카메라 Gizmo를 시각화하고 Waypoint 편집을 지원한다.
- **Crowd Debug Window**: `Window ▸ Forge ▸ Crowd Monitor`에서 현재 Agents 수, 행동 상태, Occlusion 비율을 실시간으로 표시한다. 이 값은 `FrameContext.PersonStates`를 그대로 시각화하여 디버깅 시 문서/레이블을 동시에 비교할 수 있다.
- **Scene Asset Validator**: `Assets ▸ Forge ▸ Validate Scene Asset` 메뉴로 업로드 전 metadata를 미리 점검한다. `docs/design/14_Scene_Asset_Registry.md`의 검증 규칙을 불러와 오프라인에서 동일한 결과를 미리 확인한다.

Editor 확장 코드는 `Packages/com.forge.simulation/Editor`에 위치시키고, 모든 UI는 UIToolkit 기반으로 작성해 Headless 테스트에서도 자동 검증할 수 있도록 한다.

---

## 4. Remote 모드 (Phase 2)

### 4.1 개념

Unity가 **별도 프로세스**로 실행되고, C# Orchestration이 HTTP REST API로 통신한다.

```
┌─────────────────────┐         HTTP          ┌─────────────────────┐
│ C# Orchestration    │ ◄─────────────────► │ Unity Standalone    │
│ (SessionManager)    │   localhost:8080     │ (REST API Server)   │
└─────────────────────┘                       └─────────────────────┘
```

### 4.2 Unity 빌드 방법

#### Step 1: REST API Server 추가

**UnityHttpServer.cs**:
```csharp
using UnityEngine;
using System.Net;
using System.Threading.Tasks;

public class UnityHttpServer : MonoBehaviour
{
    private HttpListener _listener;
    private SimulationController _controller;
    private readonly string _apiKey = Environment.GetEnvironmentVariable("FORGE_API_KEY");

    void Start()
    {
        _controller = GetComponent<SimulationController>();

        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:8080/");
        _listener.Start();

        Task.Run(() => ListenAsync());

        Debug.Log("Unity HTTP Server started on port 8080");
    }

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            var context = await _listener.GetContextAsync();
            await HandleRequestAsync(context);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        if (!IsAuthorized(request))
        {
            response.StatusCode = 401;
            response.Close();
            return;
        }

        // POST /session/init
        if (request.Url.AbsolutePath == "/session/init" && request.HttpMethod == "POST")
        {
            var config = ParseConfig(request.InputStream);
            await _controller.InitializeAsync(config);

            response.StatusCode = 200;
            response.Close();
        }

        // POST /session/start
        else if (request.Url.AbsolutePath == "/session/start" && request.HttpMethod == "POST")
        {
            await _controller.StartAsync();

            WriteJson(response, new { status = "success" });
        }

        // POST /session/frame (Forge Simulation API 확장)
        else if (request.Url.AbsolutePath == "/session/frame" && request.HttpMethod == "POST")
        {
            var body = new StreamReader(request.InputStream).ReadToEnd();
            var payload = JsonUtility.FromJson<FrameRequest>(body);
            var frame = await _controller.GenerateFrameAsync(payload.FrameId);
            WriteJson(response, frame);
        }

        // GET /status
        else if (request.Url.AbsolutePath == "/status" && request.HttpMethod == "GET")
        {
            var status = _controller.GetStatusSummary();
            WriteJson(response, status);
        }

        // POST /session/stop
        else if (request.Url.AbsolutePath == "/session/stop" && request.HttpMethod == "POST")
        {
            await _controller.StopAsync();
            WriteJson(response, new { status = "success" });
        }
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        if (string.IsNullOrEmpty(_apiKey)) return true;
        return request.Headers["X-Api-Key"] == _apiKey;
    }

    private static void WriteJson(HttpListenerResponse response, object payload)
    {
        var json = JsonUtility.ToJson(payload);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    [Serializable]
    private class FrameRequest
    {
        public int FrameId;
    }
}
```

#### Step 2: Standalone Build

```bash
# Unity Build (Headless 포함)
unity-editor \
  -quit \
  -batchmode \
  -buildTarget StandaloneLinux64 \
  -executeMethod Forge.BuildScripts.BuildRemoteMode
```

**BuildRemoteMode.cs**:
```csharp
public static class BuildScripts
{
    [MenuItem("Forge/Build Remote Mode")]
    public static void BuildRemoteMode()
    {
        var scenes = new[] { "Assets/Scenes/RemoteServer.unity" };

        BuildPipeline.BuildPlayer(
            scenes,
            "Builds/forge-unity-server",
            BuildTarget.StandaloneLinux64,
            BuildOptions.EnableHeadlessMode  // ← Headless
        );
    }
}
```

#### Step 3: Unity 서버 실행

```bash
# Foreground 실행
./forge-unity-server -logFile -

# Background 실행 (프로덕션)
nohup ./forge-unity-server -logFile /var/log/unity-server.log &
```

---

### 4.3 HttpSimulationGateway 구현 (C# 측)

```csharp
public class HttpSimulationGateway : ISimulationGateway
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public HttpSimulationGateway(string baseUrl = "http://localhost:8080", string? apiKey = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }
    }

    public async Task InitializeAsync(SessionConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        await PostJsonAsync("/session/init", json);
    }

    public async Task<FrameGenerationResult> GenerateFrameAsync(int frameId)
    {
        var json = await PostJsonAsync("/session/frame", JsonSerializer.Serialize(new { frameId }));
        return JsonSerializer.Deserialize<FrameGenerationResult>(json)!;
    }

    public async Task<SceneState> GetCurrentSceneStateAsync()
    {
        var json = await _httpClient.GetStringAsync($"{_baseUrl}/status");
        return JsonSerializer.Deserialize<SceneState>(json)!;
    }

    public void Shutdown()
    {
        _ = PostJsonAsync("/session/stop", "{}");
    }

    private async Task<string> PostJsonAsync(string path, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}{path}", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

---

### 4.4 Config 예시

```json
{
  "sessionId": "remote_test_001",
  "totalFrames": 50000,
  "simulationGateway": {
    "mode": "remote",
    "host": "127.0.0.1",
    "port": 8080,
    "auth": { "type": "api-key", "apiKeyEnv": "FORGE_API_KEY" },
    "allowedHosts": ["127.0.0.1"]
  },
  "scenes": [
    { "name": "Factory", "durationFrames": 50000 }
  ],
  "cameras": [
    { "id": "cam01", "type": "static", "position": [0,2,-5], "rotation": [0,0,0], "resolution": "1920x1080" }
  ],
  "crowd": { "minCount": 20, "maxCount": 30 },
  "timeWeather": { "timeOfDay": "day", "brightness": 1.0 },
  "output": { "imageFormat": "jpg", "labelFormats": ["json"] }
}
```

---

## 5. Headless 모드 (Phase 3)

### 5.1 개념

GUI 없이 Unity를 **서버 모드**로 실행. GPU는 사용하지만 화면 출력 없음.

**사용 사례**:
- 클라우드 서버 (AWS EC2, GCP Compute Engine)
- Docker 컨테이너
- CI/CD 파이프라인

### 5.2 빌드 방법

```csharp
BuildPipeline.BuildPlayer(
    scenes,
    "Builds/forge-unity-headless",
    BuildTarget.StandaloneLinux64,
    BuildOptions.EnableHeadlessMode  // ← Headless 활성화
);
```

### 5.3 실행 예시

```bash
# Headless 실행
./forge-unity-headless \
  -batchmode \
  -nographics \
  -logFile /var/log/unity.log

# Docker 컨테이너
docker run -d \
  --gpus all \
  -p 8080:8080 \
  forge-unity-headless:latest
```

### 5.4 GPU 사용 확인

```bash
# NVIDIA GPU 사용 확인
nvidia-smi

# Unity에서 GPU 활성화 확인
grep "GfxDevice" /var/log/unity.log
# 출력: GfxDevice: NVIDIA GeForce RTX 3080
```

---

## 6. Unity Time vs Simulation Time

### 6.1 시간 모델

| 시간 유형 | 정의 | 용도 |
|----------|------|------|
| **Real Time** | 실제 경과 시간 | FPS 측정, 벤치마크 |
| **Unity Time** | `Time.time` (Unity 내부) | 물리/애니메이션 |
| **Simulation Time** | FrameContext.Timestamp | 데이터 라벨, 재현성 |

### 6.2 구현 예시

```csharp
public class TimeManager : MonoBehaviour
{
    private float _simulationTime = 0f;
    private int _currentFrame = 0;

    void FixedUpdate()
    {
        // 고정 시간 간격 (재현성 보장)
        float deltaTime = Time.fixedDeltaTime;  // 0.02초 (50 FPS)

        _simulationTime += deltaTime;
        _currentFrame++;

        // FrameContext 생성
        var frameContext = new FrameContext
        {
            FrameId = _currentFrame,
            Timestamp = _simulationTime,  // ← Simulation Time
            RealTime = Time.realtimeSinceStartup  // ← Real Time (optional)
        };
    }
}
```

### 6.3 재현성 보장

```json
// 동일한 Config → 동일한 Timestamp 시퀀스
{
  "simulation": {
    "fixedDeltaTime": 0.02,
    "timeScale": 1.0,
    "randomSeed": 42  // ← 재현성 보장
  }
}
```

```csharp
void Start()
{
    Time.fixedDeltaTime = config.Simulation.FixedDeltaTime;
    Time.timeScale = config.Simulation.TimeScale;
    Random.InitState(config.Simulation.RandomSeed);
}
```

---

## 7. Unity Main Thread 제약 처리

### 7.1 문제점

Unity API는 **Main Thread에서만** 호출 가능:
- `Camera.Render()`
- `GameObject.Instantiate()`
- `NavMesh.SamplePosition()`

Worker Thread에서 호출 시 **Exception**:
```
UnityException: get_transform can only be called from the main thread
```

### 7.2 해결책: Main Thread Dispatcher

```csharp
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _executionQueue = new();

    void Awake()
    {
        _instance = this;
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public static Task<T> RunOnMainThread<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();

        lock (_instance._executionQueue)
        {
            _instance._executionQueue.Enqueue(() =>
            {
                try
                {
                    var result = action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
        }

        return tcs.Task;
    }
}
```

**사용 예시**:
```csharp
// CaptureWorker.cs (C# Pipeline)
public async Task<byte[]> CaptureImageAsync(Camera camera)
{
    // Main Thread에서 실행
    return await UnityMainThreadDispatcher.RunOnMainThread(() =>
    {
        camera.Render();
        return ReadPixels(camera);
    });
}
```

---

## 8. Unity 프로젝트 & Scene 구성

### 8.0 Unity 프로젝트 구조 예시

```
Forge.Unity/
├── Assets/
│   ├── Scenes/
│   │   ├── Factory/
│   │   │   ├── Factory_Main.unity
│   │   │   └── LightingSettings.asset
│   │   ├── Office/
│   │   └── Warehouse/
│   ├── Scripts/
│   │   ├── Simulation/
│   │   └── Services/
│   ├── Prefabs/
│   ├── AddressableAssetsData/
│   └── Resources/
├── Packages/
│   └── manifest.json
└── ProjectSettings/
    ├── GraphicsSettings.asset
    └── InputManager.asset
```

#### 8.0.1 필수 패키지 및 버전

| 패키지 | 목적 | 권장 버전 |
|--------|------|-----------|
| `com.unity.render-pipelines.universal` | URP 렌더링 | 14.x (Unity 2022 LTS) |
| `com.unity.perception` | Synthetic 라벨링/랜덤화 | 1.0.0-pre.3 이상 |
| `com.unity.addressables` | Asset 로딩/배포 | 1.21.x |
| `com.unity.ai.navigation` | NavMesh 빌드/업데이트 | 1.1.x |
| `com.unity.cinemachine` | 카메라 경로/트래킹 | 2.9.x |

`Packages/manifest.json` 스니펫:

```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "14.0.9",
    "com.unity.perception": "1.0.0-pre.3",
    "com.unity.addressables": "1.21.19",
    "com.unity.ai.navigation": "1.1.4",
    "com.unity.cinemachine": "2.9.7"
  }
}
```

#### 8.0.2 Scene 설정 체크리스트

- [ ] Scene이 Build Settings에 등록되어 있는가?
- [ ] Lighting Settings에서 Auto Generate 비활성화 후 `Generate Lighting` 실행
- [ ] NavMesh Bake가 최신인지 (`Navigation` 창에서 확인)
- [ ] Scene Root에 `AppEntrypoint`, `CameraService`, `CrowdService`, `EnvironmentService` 프리팹이 포함되어 있는가?
- [ ] Addressable Label(`scene_factory`, `scene_office` 등)이 적용되었는가?
- [ ] Scene Metadata ScriptableObject에 좌표계/단위/NavMesh 정보가 채워졌는가?

체크리스트는 `SceneValidationEditorTests`로 자동화하는 것을 권장한다.

#### 8.0.3 Perception Package 통합

1. Package Manager에서 Perception 패키지를 설치한다.
2. `Perception Camera` 컴포넌트를 카메라 프리팹에 붙이고 `BoundingBox3DLabeler`, `InstanceSegmentationLabeler` 등을 활성화한다.
3. `LabelingConfiguration` ScriptableObject를 생성해 라벨 ID를 정의한다.
4. `PerceptionDatasetWriter`를 `SimulationScenario`에 연결해 라벨 파일 출력 경로를 지정한다.
5. Perception 이벤트를 Forge `FrameContext`로 변환하는 `PerceptionBridge` 스크립트를 추가한다.

#### 8.0.4 Asset 관리 전략 (Addressables)

- Scene/Prefabs/머티리얼을 Addressables로 등록해 Standalone/Headless 빌드에서 동일 버전 사용을 보장한다.
- Addressables Profile을 `Editor`, `Standalone`, `Headless`로 분리하고 CI에서 `BuildScriptPackedMode`를 사용한다.
- 사용자 업로드 Scene Asset은 `SceneAssetRegistry` Editor 툴로 Addressable Group에 자동 추가한다.

#### 8.0.5 Build Pipeline 단계

1. **PreBuild**: `SceneValidationEditorTests`, Addressable Analyze 실행
2. **Addressables Build**: `AddressableAssetSettings.BuildPlayerContent()` 수행
3. **Player Build**: `BuildPipeline.BuildPlayer`로 Standalone/Headless 빌드
4. **PostBuild**: `BuildInfo.json`에 Perception Labeler/패키지 버전을 기록
5. **Packaging**: 빌드 산출물 + Addressables Bundle을 `dist/forge-unity-{mode}.tar.gz`로 압축

CI 예시:

```bash
unity-editor -quit -batchmode \
  -projectPath Forge.Unity \
  -executeMethod Forge.BuildScripts.BuildRemoteMode \
  -customBuildPath Builds/remote/Linux64/ForgeUnity.x86_64 \
  -logFile builds.log
```

### 8.1 MainScene.unity

```
MainScene
├── AppEntrypoint (MonoBehaviour)
│   └── 역할: SessionManager/GenerationController 시작
│
├── SimulationGateway (MonoBehaviour)
│   └── InProcessSimulationGateway 또는 SimulationController
│
├── Services
│   ├── CameraService
│   ├── CrowdService
│   ├── EnvironmentService
│   └── TimeWeatherService
│
├── Environment
│   ├── Factory Scene Root
│   ├── NavMesh
│   └── SpawnZones
│
└── Cameras
    ├── Camera01 (Static)
    ├── Camera02 (Static)
    └── BotCamera01 (Mobile)
```

### 8.2 Prefab 구조

```
Prefabs/
├── Person.prefab
│   ├── Model (SkinnedMeshRenderer)
│   ├── NavMeshAgent
│   └── PersonController.cs
│
├── StaticCamera.prefab
│   ├── Camera Component
│   └── CameraController.cs
│
└── MobileCamera.prefab
    ├── Camera Component
    ├── MobileCameraController.cs
    └── PathFollower.cs
```

---

## 9. Phase별 구현 가이드

### Phase 1: InProcess (Editor Playmode)

```bash
# 1. Unity 프로젝트 생성
unity-hub --createProject forge-unity

# 2. Scene 구성
# - MainScene.unity 생성
# - AppEntrypoint.cs 추가

# 3. C# Orchestration 연결
# - SessionManager, GenerationController 구현
# - InProcessSimulationGateway 연결

# 4. Playmode 실행
# - Play 버튼 클릭
# - Frame 생성 확인
```

### Phase 2: Remote (Standalone Build)

```bash
# 1. HTTP Server 추가
# - UnityHttpServer.cs 구현
# - 5_API Specification.md 참조

# 2. Standalone Build
unity-editor -quit -batchmode \
  -executeMethod Forge.BuildScripts.BuildRemoteMode

# 3. Unity 서버 실행
./forge-unity-server -logFile -

# 4. C# Orchestration 실행
dotnet run --project src/Application -- \
  --config remote_config.json
```

### Phase 3: Headless (서버 배포)

```bash
# 1. Headless Build
unity-editor -quit -batchmode \
  -executeMethod Forge.BuildScripts.BuildHeadlessMode

# 2. Docker 이미지 빌드
docker build -t forge-unity-headless .

# 3. 클라우드 배포
kubectl apply -f k8s/unity-deployment.yaml
```

---

## 10. 테스트 전략

### 10.1 Unity Playmode Test

```csharp
using UnityEngine.TestTools;
using NUnit.Framework;

public class SimulationGatewayTests
{
    [UnityTest]
    public IEnumerator GenerateFrame_ReturnsValidFrameContext()
    {
        // Arrange
        var gateway = new GameObject().AddComponent<InProcessSimulationGateway>();
        var config = TestHelper.CreateBasicConfig();

        yield return gateway.InitializeAsync(config);

        // Act
        var frame = gateway.GenerateFrameAsync(0).Result;

        // Assert
        Assert.IsNotNull(frame);
        Assert.AreEqual(0, frame.FrameId);
        Assert.Greater(frame.PersonStates.Count, 0);
    }
}
```

### 10.2 Integration Test (Batchmode)

```bash
# Unity Batchmode Test 실행
unity-editor \
  -batchmode \
  -runTests \
  -testPlatform PlayMode \
  -testResults results.xml
```

---

## 11. 문제 해결 (Troubleshooting)

### 11.1 "Main Thread Exception"

**증상**:
```
UnityException: Camera.Render can only be called from the main thread
```

**해결**:
```csharp
// UnityMainThreadDispatcher 사용
await UnityMainThreadDispatcher.RunOnMainThread(() =>
{
    Camera.main.Render();
});
```

### 11.2 "HTTP Connection Refused"

**증상**:
```
HttpRequestException: Connection refused (localhost:8080)
```

**해결**:
1. Unity 서버 실행 확인: `ps aux | grep unity`
2. 포트 사용 확인: `lsof -i :8080`
3. 방화벽 확인: `sudo ufw allow 8080`

### 11.3 "Scene Load Failed"

**증상**:
```
Scene 'Factory' not found
```

**해결**:
```csharp
// Build Settings에 Scene 추가
File → Build Settings → Add Open Scenes

// 또는 코드에서 확인
if (SceneManager.GetSceneByName("Factory").IsValid())
{
    SceneManager.LoadScene("Factory");
}
```

---

## 12. 참고 자료

- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)
- [Unity Build Pipeline](https://docs.unity3d.com/Manual/BuildPlayerPipeline.html)
- [Unity Headless Mode](https://docs.unity3d.com/Manual/CommandLineArguments.html)
- [Unity REST Server 예제](https://github.com/Unity-Technologies/UnityHTTP)

---

**문서 작성일**: 2025-11-15
**다음 업데이트**: Phase 1 구현 완료 후
