
## 1. 목적

Forge의 Checkpoint 및 복구 메커니즘을 정의한다.

**핵심 목표**:
- 장시간 세션 중 예상치 못한 중단 발생 시 복구 가능
- 재시작 시 이미 생성된 프레임 재사용
- 최소한의 성능 오버헤드

**Phase**:
- Phase 1: Checkpoint 미지원 (선택적 수동 복구만)
- Phase 2: 기본 Checkpoint 지원
- Phase 3: Advanced Checkpoint (분산 환경, 증분 저장)

---

## 2. Checkpoint 데이터 구조

### 2.1 Checkpoint 파일 구조

```
/output/session_xxx/checkpoints/
├── checkpoint_frame_010000.json
├── checkpoint_frame_020000.json
├── checkpoint_frame_030000.json
└── latest.json  → checkpoint_frame_030000.json (심볼릭 링크)
```

### 2.2 Checkpoint JSON 스키마

```json
{
  "$schema": "checkpoint-v1.0.json",
  "checkpointVersion": "1.0.0",
  "createdAt": "2023-10-27T15:30:45Z",
  "sessionId": "session_factory_001",
  "currentFrame": 30000,
  "totalFrames": 100000,
  "sessionStatus": "running",

  "sessionContext": {
    "configPath": "/path/to/config.json",
    "outputDirectory": "/data/output/session_factory_001",
    "startedAt": "2023-10-27T12:00:00Z",
    "elapsedSeconds": 12645
  },

  "scenarioState": {
    "currentSceneIndex": 0,
    "sceneName": "Factory",
    "sceneStartFrame": 0,
    "sceneEndFrame": 100000
  },

  "pipelineState": {
    "lastCapturedFrame": 30050,
    "lastDetectedFrame": 30020,
    "lastTrackedFrame": 30010,
    "lastStoredFrame": 30000,
    "queueDepths": {
      "capture": 50,
      "detection": 30,
      "tracking": 20,
      "encode": 15,
      "storage": 10
    }
  },

  "trackingState": {
    "cameras": [
      {
        "cameraId": "cam01",
        "nextTrackId": 125,
        "activeTracks": [
          {
            "trackId": 101,
            "globalPersonId": 5,
            "lastSeenFrame": 29998,
            "bbox": {"xmin": 540, "ymin": 320, "xmax": 680, "ymax": 710}
          },
          {
            "trackId": 110,
            "globalPersonId": 12,
            "lastSeenFrame": 29995,
            "bbox": {"xmin": 720, "ymin": 280, "xmax": 840, "ymax": 650}
          }
        ]
      },
      {
        "cameraId": "cam02",
        "nextTrackId": 98,
        "activeTracks": [...]
      }
    ]
  },

  "crowdState": {
    "activePersons": [
      {
        "globalPersonId": 5,
        "position": [10.5, 0, -8.3],
        "velocity": [0.8, 0, 0.2],
        "behavior": "walk",
        "spawnedAt": 1500
      },
      {
        "globalPersonId": 12,
        "position": [15.2, 0, 5.1],
        "velocity": [0, 0, 0],
        "behavior": "idle",
        "spawnedAt": 2300
      }
    ],
    "nextPersonId": 25
  },

  "statistics": {
    "totalFramesGenerated": 30000,
    "totalDetections": 1245000,
    "avgFps": 8.5,
    "peakMemoryMB": 3200,
    "diskUsageGB": 45.2
  }
}
```

---

## 3. Checkpoint 저장 정책

### 3.1 자동 저장 트리거

**프레임 수 기반** (기본):
```
intervalFrames = 10,000 (configurable)

if (currentFrame % intervalFrames == 0):
    SaveCheckpoint()
```

**시간 기반** (선택):
```
intervalMinutes = 30 (configurable)

if (elapsedMinutes % intervalMinutes == 0):
    SaveCheckpoint()
```

**오류 발생 직전** (자동):
```
try:
    GenerateFrame()
except Exception as e:
    SaveCheckpoint(emergency=True)
    throw
```

### 3.2 Checkpoint 파일명 규칙

```
checkpoint_frame_{currentFrame:06d}.json

예:
checkpoint_frame_010000.json
checkpoint_frame_020000.json
checkpoint_frame_030000.json
```

### 3.3 저장 위치

```
{sessionOutputDirectory}/checkpoints/
```

### 3.4 보관 정책

**기본**:
- 최근 3개 checkpoint만 유지
- 나머지는 자동 삭제

**Advanced (Phase 3)**:
- 마일스톤 checkpoint 영구 보관 (예: 100k, 500k, 1M)
- 압축 저장 (gzip)

---

## 4. Checkpoint 저장 프로세스

### 4.1 저장 순서

```csharp
public class CheckpointManager
{
    public void SaveCheckpoint(SessionContext session, int currentFrame)
    {
        var checkpoint = new Checkpoint
        {
            CheckpointVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            SessionId = session.Config.SessionId,
            CurrentFrame = currentFrame,
            TotalFrames = session.Config.TotalFrames
        };

        // 1. SessionContext 저장
        checkpoint.SessionContext = SerializeSessionContext(session);

        // 2. ScenarioState 저장
        checkpoint.ScenarioState = _scenarioManager.GetCurrentState();

        // 3. PipelineState 저장
        checkpoint.PipelineState = _pipelineCoordinator.GetState();

        // 4. TrackingState 저장
        checkpoint.TrackingState = _trackingWorker.GetState();

        // 5. CrowdState 저장 (Unity에서 가져오기)
        checkpoint.CrowdState = _crowdService.GetState();

        // 6. Statistics 저장
        checkpoint.Statistics = _statsService.GetCurrentStats();

        // 7. 파일 쓰기 (atomic)
        var tempPath = Path.Combine(checkpointDir, $"checkpoint_temp_{currentFrame}.json");
        var finalPath = Path.Combine(checkpointDir, $"checkpoint_frame_{currentFrame:D6}.json");

        File.WriteAllText(tempPath, JsonSerializer.Serialize(checkpoint, _jsonOptions));
        File.Move(tempPath, finalPath, overwrite: true);

        // 8. latest.json 심볼릭 링크 업데이트
        UpdateLatestLink(finalPath);

        // 9. 오래된 checkpoint 정리
        CleanupOldCheckpoints(keepCount: 3);

        _logger.LogInformation($"Checkpoint saved: frame {currentFrame}");
    }
}
```

### 4.2 저장 시 성능 고려사항

**비동기 저장**:
```csharp
// Main thread는 블로킹하지 않음
Task.Run(() => SaveCheckpoint(session, currentFrame));
```

**저장 중 세션 계속 진행**:
- Checkpoint는 현재 상태의 스냅샷
- 저장 중에도 새 프레임 생성 가능
- 단, 동시에 여러 checkpoint 저장 금지 (lock)

**예상 오버헤드**:
- Checkpoint 파일 크기: ~1MB (인원 100명 기준)
- 저장 시간: <1초
- FPS 영향: 무시할 수준 (비동기 처리)

---

## 5. 복구 (Resume) 프로세스

### 5.1 복구 트리거

**CLI 옵션**:
```bash
dotnet run --resume /output/session_xxx/checkpoints/latest.json
```

**자동 감지**:
```csharp
// 세션 시작 시 자동으로 checkpoint 존재 여부 확인
if (File.Exists(latestCheckpointPath))
{
    Console.WriteLine("Checkpoint found. Resume? (y/n)");
    var input = Console.ReadLine();
    if (input == "y")
    {
        ResumeFromCheckpoint(latestCheckpointPath);
    }
}
```

### 5.2 복구 절차

```csharp
public SessionContext ResumeFromCheckpoint(string checkpointPath)
{
    // 1. Checkpoint 로드
    var checkpoint = LoadCheckpoint(checkpointPath);

    // 2. SessionConfig 로드
    var config = LoadConfig(checkpoint.SessionContext.ConfigPath);

    // 2-1. 마지막으로 **저장 완료**된 프레임 기준 계산
    var lastStoredFrame = checkpoint.PipelineState?.LastStoredFrame ?? checkpoint.CurrentFrame;

    // 3. SessionContext 복원
    var session = new SessionContext
    {
        Config = config,
        SessionDirectory = checkpoint.SessionContext.OutputDirectory,
        CurrentFrame = lastStoredFrame,
        StartedAt = checkpoint.SessionContext.StartedAt
    };

    // 4. ScenarioManager 상태 복원
    _scenarioManager.RestoreState(checkpoint.ScenarioState);

    // 5. EnvironmentCoordinator: Scene 재로드
    _envCoordinator.ActivateScene(checkpoint.ScenarioState.SceneName);

    // 6. CrowdService: 인물 재생성
    _crowdService.RestoreState(checkpoint.CrowdState);

    // 7. TrackingWorker: track 상태 복원
    _trackingWorker.RestoreState(checkpoint.TrackingState);

    // 8. PipelineCoordinator: Queue 비우기 (fresh start)
    _pipelineCoordinator.Reset();

    // 9. 다른 설정/메타 복원
    _metadataRestorer?.Restore(checkpoint.SessionContext);

    // 10. 다음 프레임부터 시작 (마지막 저장 프레임 이후)
    session.CurrentFrame = Math.Min(lastStoredFrame, checkpoint.CurrentFrame) + 1;

    _logger.LogInformation($"Resumed from checkpoint: frame {session.CurrentFrame - 1} (last stored)");
    return session;
}
```

**시퀀스 다이어그램(개념)**:
```
User/CLI → SessionManager → CheckpointManager: Load checkpoint
CheckpointManager → ValidationService: Verify files(images/labels)/config
CheckpointManager → ScenarioManager/CrowdService/TrackingWorker: Restore state
CheckpointManager → PipelineCoordinator: Reset queues
SessionManager: Resume at lastStoredFrame + 1
```

### 5.3 복구 시 검증

```csharp
private void ValidateCheckpoint(Checkpoint checkpoint)
{
    // 1. 버전 호환성 확인
    if (checkpoint.CheckpointVersion != SupportedVersion)
        throw new Exception("Incompatible checkpoint version");

    // 2. 파일 무결성 확인
    var outputDir = checkpoint.SessionContext.OutputDirectory;
    if (!Directory.Exists(outputDir))
        throw new Exception("Output directory not found");

    // 3. 생성된 프레임 수 확인 (마지막 저장 프레임 기준, 전 카메라/라벨 검증)
    var expectedFrames = checkpoint.PipelineState?.LastStoredFrame ?? checkpoint.CurrentFrame;

    foreach (var cameraDir in Directory.GetDirectories(Path.Combine(outputDir, "images")))
    {
        var actualFrames = Directory.GetFiles(cameraDir, "*.jpg").Length;
        if (actualFrames < expectedFrames - 50) // 카메라별 허용 오차 50
            _logger.LogWarning($"Frame mismatch ({Path.GetFileName(cameraDir)}): expected {expectedFrames}, found {actualFrames}");
    }

    var labelDir = Path.Combine(outputDir, "labels");
    if (Directory.Exists(labelDir))
    {
        var labelFiles = Directory.GetFiles(labelDir, "*.json", SearchOption.AllDirectories).Length;
        if (labelFiles < expectedFrames - 50) // 최소 프레임 수만큼 라벨이 있어야 함 (카메라당 1개 이상 권장)
            _logger.LogWarning($"Label count suspicious: expected at least {expectedFrames}, found {labelFiles}");
    }

    // 4. Config 파일 존재 확인
    if (!File.Exists(checkpoint.SessionContext.ConfigPath))
        throw new Exception("Config file not found");
}
```

---

## 6. 특수 상황 처리

### 6.1 Scene 전환 중 복구

```csharp
// Checkpoint 시점이 scene 전환 직후라면
if (checkpoint.ScenarioState.CurrentSceneIndex != previousIndex)
{
    // Scene 재로드
    _envCoordinator.ActivateScene(checkpoint.ScenarioState.SceneName);

    // Crowd 재생성 (새 scene이므로)
    _crowdService.RestoreState(checkpoint.CrowdState);
}
```

### 6.2 Pipeline 큐 불일치

```csharp
// Checkpoint 시점의 큐 상태와 현재 상태가 다를 수 있음
// → 복구 시 모든 큐를 비우고 fresh start

_pipelineCoordinator.Reset();

// 단, 이미 저장된 프레임은 재생성하지 않음
var lastStoredFrame = checkpoint.PipelineState.LastStoredFrame;
session.CurrentFrame = lastStoredFrame + 1;
```

### 6.3 TrackID 불일치 방지

```csharp
// 복구 시 각 카메라의 nextTrackId를 정확히 복원
foreach (var camera in checkpoint.TrackingState.Cameras)
{
    _trackingWorker.SetNextTrackId(camera.CameraId, camera.NextTrackId);

    // Active tracks 복원
    foreach (var track in camera.ActiveTracks)
    {
        _trackingWorker.RestoreTrack(camera.CameraId, track);
    }
}
```

---

## 7. Phase별 Checkpoint 기능

### Phase 1: 최소 복구 (Minimal Recovery)

**목표**: 중단된 세션의 **프레임 번호**만 복구하여 재시작

**범위**:
- ✅ Checkpoint 저장: 기본 메타데이터만 (JSON)
  - session_id
  - current_frame (마지막 **저장 완료된** 프레임)
  - total_frames
  - scene_name
  - output_directory
  - global_person_id_count (다음 할당 ID)

- ✅ 복구: 자동 (CLI 옵션 또는 프롬프트)
  - 마지막 저장 프레임 이후부터 재시작
  - Scene 재로드
  - Crowd 새로 생성 (Appearance 랜덤)

**복원되지 않는 것**:
- ❌ Tracking 상태 (track ID 0부터 재시작)
- ❌ Crowd 위치/Behavior (새로 생성)
- ❌ Pipeline Queue 상태

**Checkpoint 파일 예시**:
```json
{
  "checkpointVersion": "1.0.0",
  "createdAt": "2023-10-27T15:30:45Z",
  "sessionId": "session_factory_001",
  "currentFrame": 30000,
  "totalFrames": 100000,
  "sessionContext": {
    "configPath": "/path/to/config.json",
    "outputDirectory": "/data/output/session_factory_001"
  },
  "scenarioState": {
    "currentSceneIndex": 0,
    "sceneName": "Factory"
  },
  "statistics": {
    "avgFps": 8.5,
    "totalDetections": 1245000
  }
}
```

**저장 간격**: 10,000 프레임마다 (약 20분 @ 8 FPS)

**복구 절차**:
```csharp
public SessionContext ResumeFromCheckpoint(string checkpointPath)
{
    var checkpoint = LoadCheckpoint(checkpointPath);

    // 1. Config 재로드
    var config = LoadConfig(checkpoint.SessionContext.ConfigPath);

    // 2. Session 재생성
    var session = new SessionContext
    {
        Config = config,
        SessionDirectory = checkpoint.SessionContext.OutputDirectory,
        CurrentFrame = checkpoint.CurrentFrame + 1  // 다음 프레임부터
    };

    // 3. Scene 재로드 (Unity)
    _envCoordinator.ActivateScene(checkpoint.ScenarioState.SceneName);

    // 4. Crowd 새로 생성 (Tracking은 reset)
    _crowdService.SpawnInitialCrowd(config.Crowd);

    return session;
}
```

**용도**:
- ✅ 개발/디버깅 중 빠른 재시작
- ✅ 치명적 오류 후 부분 복구 (일부 데이터 손실 허용)
- ❌ 프로덕션 환경 (데이터 품질 요구 높음)

---

### Phase 2: 상태 복원 (State Recovery)

**목표**: Tracking/Crowd 상태까지 정확히 복원

**범위** (Phase 1 +):
- ✅ Tracking 상태 복원
  - 카메라별 nextTrackId
  - Active tracks (trackId, globalPersonId, lastSeenFrame, bbox)

- ✅ Crowd 상태 복원
  - Active persons (globalPersonId, position, velocity, behavior, appearance)
  - nextPersonId

**Checkpoint 파일 예시** (추가 부분):
```json
{
  "trackingState": {
    "cameras": [
      {
        "cameraId": "cam01",
        "nextTrackId": 125,
        "activeTracks": [
          {
            "trackId": 101,
            "globalPersonId": 5,
            "lastSeenFrame": 29998,
            "bbox": {"xmin": 540, "ymin": 320, "xmax": 680, "ymax": 710}
          }
        ]
      }
    ]
  },
  "crowdState": {
    "activePersons": [
      {
        "globalPersonId": 5,
        "position": [10.5, 0, -8.3],
        "velocity": [0.8, 0, 0.2],
        "behavior": "walk",
        "appearance": {"model": "person_01", "texture": "casual_blue"}
      }
    ],
    "nextPersonId": 25
  }
}
```

**복구 절차** (Phase 1 +):
```csharp
// 5. Tracking 상태 복원
_trackingWorker.RestoreState(checkpoint.TrackingState);

// 6. Crowd 상태 복원 (위치/Appearance 유지)
_crowdService.RestoreState(checkpoint.CrowdState);
```

**파일 크기**: ~1~2 MB (인원 100명 기준)

**저장 간격**: 10,000 프레임마다 (Phase 1과 동일)

**용도**:
- ✅ 프로덕션 환경 (데이터 품질 중요)
- ✅ Scene 전환 중 복구
- ✅ Global ID 연속성 보장

---

### Phase 3: 완전 복구 (Full Recovery)

**목표**: Pipeline Queue 상태까지 복원 (중단 직전 상태로)

**범위** (Phase 2 +):
- ✅ Pipeline 상태 복원
  - 각 Worker의 Queue depth
  - 처리 중인 프레임 목록
  - Back-pressure 상태

- ✅ 증분 저장 (Delta Checkpoint)
  - 변경된 상태만 저장 (압축률 10배↑)

- ✅ 분산 환경 지원
  - 여러 Worker 노드의 상태 통합

- ✅ 클라우드 저장소 연동
  - S3/GCS 자동 업로드

**Checkpoint 파일 예시** (추가 부분):
```json
{
  "pipelineState": {
    "lastCapturedFrame": 30050,
    "lastDetectedFrame": 30020,
    "lastTrackedFrame": 30010,
    "lastStoredFrame": 30000,
    "queueDepths": {
      "capture": 50,
      "detection": 30,
      "tracking": 20
    },
    "pendingFrames": [30001, 30002, ...]
  }
}
```

**파일 크기**: ~5~10 MB (압축 전), ~500 KB (gzip 압축 후)

**저장 간격**: 5,000 프레임마다 (더 자주 저장)

**용도**:
- ✅ 고가용성 환경 (중단 허용 안 함)
- ✅ 클라우드 분산 처리
- ✅ 장시간 실행 세션 (1M+ 프레임)

---

### Phase별 비교표

| 항목 | Phase 1 | Phase 2 | Phase 3 |
|------|---------|---------|---------|
| **프레임 번호 복구** | ✅ | ✅ | ✅ |
| **Tracking 상태** | ❌ (reset) | ✅ | ✅ |
| **Crowd 상태** | ❌ (재생성) | ✅ | ✅ |
| **Pipeline Queue** | ❌ | ❌ | ✅ |
| **저장 간격** | 10,000 frame | 10,000 frame | 5,000 frame |
| **파일 크기** | ~100 KB | ~1 MB | ~500 KB (압축) |
| **복구 시간** | ~30초 | ~1분 | ~2분 |
| **데이터 손실** | 허용 | 최소 | 거의 없음 |
| **구현 복잡도** | 낮음 | 중간 | 높음 |

---

### 권장 사항

**개발 환경**:
- Phase 1 사용 (빠른 재시작, 간단함)

**소규모 프로덕션** (< 100K 프레임):
- Phase 1 사용 (충분함)

**대규모 프로덕션** (100K~500K 프레임):
- Phase 2 사용 (상태 보존 필요)

**클라우드/분산 환경** (1M+ 프레임):
- Phase 3 사용 (고가용성 요구)

---

## 8. 에러 처리

### 8.1 Checkpoint 저장 실패

```csharp
try
{
    SaveCheckpoint(session, currentFrame);
}
catch (IOException e)
{
    _logger.LogError($"Checkpoint save failed: {e.Message}");
    // 세션은 계속 진행 (checkpoint 없이)
}
```

### 8.2 Checkpoint 복구 실패

```csharp
try
{
    ResumeFromCheckpoint(checkpointPath);
}
catch (Exception e)
{
    _logger.LogError($"Checkpoint resume failed: {e.Message}");
    // 사용자에게 선택 제공
    Console.WriteLine("Resume failed. Options:");
    Console.WriteLine("1. Start from beginning");
    Console.WriteLine("2. Try another checkpoint");
    Console.WriteLine("3. Abort");
}
```

### 8.3 부분 복구

```csharp
// 일부 상태만 복원 가능한 경우
var partialResume = true;

if (!checkpoint.TrackingState)
{
    _logger.LogWarning("Tracking state missing, will restart tracking");
    partialResume = true;
}

if (partialResume)
{
    // 복원 가능한 부분만 복원
    session.CurrentFrame = checkpoint.CurrentFrame + 1;
    // Tracking은 새로 시작
    _trackingWorker.Reset();
}
```

---

## 9. 테스트 시나리오

### 9.1 정상 복구 테스트

```csharp
[Fact]
public async Task Resume_NormalCheckpoint_ContinuesGeneration()
{
    // Arrange: 30,000 프레임 생성 후 checkpoint
    var session = await GenerateFrames(30000, saveCheckpoint: true);

    // Act: Checkpoint에서 복구
    var resumed = ResumeFromCheckpoint("checkpoint_frame_030000.json");

    // Generate 추가 10,000 프레임
    await GenerateFrames(10000, session: resumed);

    // Assert: 총 40,000 프레임 존재
    var totalFrames = Directory.GetFiles(imagesDir, "*.jpg").Length;
    Assert.Equal(40000, totalFrames);
}
```

### 9.2 TrackID 일관성 테스트

```csharp
[Fact]
public void Resume_TrackingState_MaintainsTrackIds()
{
    // Arrange: Checkpoint에 track ID 101, 110 존재
    var checkpoint = LoadCheckpoint("test_checkpoint.json");

    // Act: 복구
    var session = ResumeFromCheckpoint(checkpoint);

    // Assert: 다음 track ID가 125부터 시작 (checkpoint에서 nextTrackId=125)
    var nextId = _trackingWorker.GetNextTrackId("cam01");
    Assert.Equal(125, nextId);
}
```

### 9.3 Scene 전환 중 복구 테스트

```csharp
[Fact]
public void Resume_MidSceneTransition_LoadsCorrectScene()
{
    // Arrange: Scene 전환 직후 checkpoint (Office scene)
    var checkpoint = new Checkpoint
    {
        ScenarioState = new ScenarioState
        {
            CurrentSceneIndex = 1,
            SceneName = "Office"
        }
    };

    // Act: 복구
    ResumeFromCheckpoint(checkpoint);

    // Assert: Office scene이 활성화됨
    var activeScene = _envCoordinator.GetActiveScene();
    Assert.Equal("Office", activeScene);
}
```

---

## 10. 모니터링 및 로깅

### 10.1 로그 메시지

```csharp
// Checkpoint 저장 시
_logger.LogInformation("Checkpoint saved: frame {Frame}, size {Size}KB, time {Time}ms",
    currentFrame, fileSize / 1024, elapsedMs);

// Checkpoint 복구 시
_logger.LogInformation("Resuming from checkpoint: frame {Frame}, created {CreatedAt}",
    checkpoint.CurrentFrame, checkpoint.CreatedAt);

// 복구 완료 시
_logger.LogInformation("Resume complete: starting from frame {Frame}, {Remaining} frames remaining",
    session.CurrentFrame, totalFrames - session.CurrentFrame);
```

### 10.2 메트릭

```
# Checkpoint 저장 성공/실패 카운트
forge_checkpoint_saves_total{status="success"} 5
forge_checkpoint_saves_total{status="failed"} 0

# Checkpoint 파일 크기
forge_checkpoint_file_size_bytes 1048576

# 복구 시간
forge_checkpoint_resume_duration_seconds 2.5
```

---

## 11. 참고 사항

### 11.1 Checkpoint vs Backup

**Checkpoint**:
- 실행 중 상태 저장
- 빠른 복구 목적
- 최소 데이터만 저장

**Backup**:
- 세션 전체 데이터 백업
- 장기 보관 목적
- 모든 이미지/라벨 포함

### 11.2 Checkpoint 사이즈 최적화

**현재** (~1MB):
- JSON 포맷
- 압축 없음
- 모든 active tracks 저장

**최적화 (Phase 3)**:
- Gzip 압축 → 1/5 크기
- Binary 포맷 (Protobuf) → 1/3 크기
- Delta checkpoint (변경분만) → 1/10 크기

---

## 12. FAQ

**Q: Checkpoint 주기를 어떻게 설정해야 하나요?**
A: 기본 10,000 프레임 또는 30분마다. 생성 속도에 따라 조정.

**Q: Checkpoint 저장 중 세션이 중단되면?**
A: Atomic write를 사용하므로 partial checkpoint는 생성되지 않음.

**Q: 여러 번 복구하면 어떻게 되나요?**
A: 마지막 checkpoint부터 계속 이어서 생성.

**Q: Checkpoint 없이 복구 가능한가요?**
A: Phase 1에서는 수동으로 마지막 저장된 프레임 확인 후 재시작. Phase 2+에서는 checkpoint 필수.
