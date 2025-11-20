Forge UI의 모든 화면에 대해 **세부 구조, 표시 요소, 데이터 매핑, 와이어프레임**을 정의하는 문서이다.  
16번(UX 개요), 17번(IA 구조) 문서의 상위 설계 내용을 실제 화면 단위로 구체화하는 단계이며,  
파이프라인/데이터모델/API 스펙과 1:1 매핑되도록 설계되어 있다.

---

# 1. 화면 설계 원칙 (Screen Design Principles)

- **정합성 우선**: 모든 UI 요소는 반드시 설계문서(datamodel, API spec, manifest, validation)의 필드와 대응된다.
    
- **요약 → 상세 구조**: 상위 화면에서 요약 정보, 클릭 시 더 상세한 정보 제공.
    
- **실시간성**: /status 기반의 업데이트는 최소 1–3초 주기로 반영.
    
- **관측 가능성 강화**: FPS, Backpressure, warnings, metrics 등은 시각적으로 강조.
    
- **Phase 기반 기능 확장**: Phase 2~4 기능(Screen Editor, Workers, Robotics)은 조건부로 활성화.
    

---

# 2. Dashboard Screen Specification

## 2.1 목적

- 엔진 전체 상태 조회
    
- 세션 목록 및 진행도 확인
    
- 새 세션 생성 진입점 제공
    

## 2.2 표시 요소

- Engine Status Panel
    
    - status → /status.status
        
    - engineVersion → /status.engineVersion
        
    - supportedVersions
        
    - authMode
        
    - fps (running 시)
        
    - currentFrame / targetFrame
        
    - queueDepthSummary → Backpressure 시각화
        
    - warnings[] 리스트
        
- Session List Table
    
    - sessionId
        
    - status
        
    - fps (마지막 기록)
        
    - progress (currentFrame / targetFrame)
        
    - qualityMode
        
    - updatedAt
        
- Buttons
    
    - [New Session]
        

## 2.3 와이어프레임

```
[Engine Status]
 Status: running   | FPS: 14.5
 Frame: 12,345 / 100,000
 Backpressure: 0.42 (OK)
 Active Scene: Factory
 Warnings:
  - QUEUE_BACKPRESSURE (capture=0.78)

[Sessions]
 | sessionId | status | fps | progress | quality | updated |
 |-----------|--------|-----|----------|---------|---------|
 | factory01 | running|14.5 | 12%      | strict  | 10s ago |
```

---

# 3. New Session Screen

## 3.1 목적

- 새 SessionConfig 생성 또는 파일 업로드
    
- Scene/Camera 초기 검토
    

## 3.2 표시 요소

- SessionConfig 필드 입력
    
    - sessionId
        
    - totalFrames
        
    - qualityMode (strict/relaxed)
        
    - frameRatePolicy.id
        
- Scene 선택
    
- Camera 목록 표시(CameraMeta 기반)
    

## 3.3 와이어프레임

```
[New Session]
 sessionId: [___________]
 totalFrames: [100000]
 qualityMode: ( ) strict  ( ) relaxed
 frameRatePolicy: [quality_first ▼]

 Scenes: [Factory, Office]
 Cameras:
   - cam01 (1920x1080, FOV 60)
   - cam02 (1280x720, FOV 90)

[Start Session]
```

---

# 4. Session Detail Screen

세션 단위로 전체 프로세스를 확인하는 핵심 화면.

## 4.1 Overview Tab

### 표시 요소

- status, qualityMode
    
- fps(avg)
    
- currentFrame / targetFrame
    
- queueDepthSummary → Backpressure indicator
    
- warnings[]
    
- FPS timeline 그래프
    
- Recent diagnostics
    

### 와이어프레임

```
[Session: factory01]
 Status: running | Quality: strict
 FPS(avg): 14.5
 Frame: 12345 / 100000
 Backpressure: 0.42 (OK)

[Graph: FPS timeline]
[Warnings]
```

---

## 4.2 Cameras Tab (Live Preview)

### 표시 요소

- CameraMeta 목록(cameraId, resolution, fov)
    
- 최신 Frame snapshot 이미지
    
- frame_id, timestamp
    
- person count
    
- occlusion avg
    
- Auto-refresh toggle
    
- Recent frame thumbnails
    

### 와이어프레임

```
[Cameras]
 cam01 | cam02 | bot_cam_01

[Live Preview – cam01]
 [Latest Frame JPEG]
 Frame ID: 12345
 Timestamp: 123.456
 Persons: 22
 Occlusion(avg): 0.31

 [Auto-refresh: ON]

[Recent frames]
 [12340] [12341] [12342] ...
```

---

## 4.3 Validation / Statistics Tab

### 표시 요소

- validationSummary 필드
    
    - frameDropCount
        
    - poseMissingCount
        
    - syncDriftExceeded
        
- statistics 그래프
    
    - bbox scale histogram
        
    - occlusion histogram
        
    - detection count plot
        
- Sample Frames (stride 기반)
    

### 와이어프레임

```
[Validation]
 Frame Drops: 2
 Pose Missing: 1
 Sync Drift: false

[Statistics]
 - Bbox Histogram
 - Occlusion Histogram
 - Detection Counts

[Sample Frames]
 [frame 100] [2500] [5000] [7500] ...
```

---

## 4.4 Manifest Viewer Tab

### 표시 요소

- manifest.json 구조를 표/트리 형태로 표시
    
- 주요 섹션 빠른 접근:
    
    - performanceSummary
        
    - stageStatuses
        
    - reidArtifacts
        
    - sensorArtifacts (Phase 4)
        

### 와이어프레임

```
[Manifest Viewer]
 { performanceSummary: {...},
   stageStatus: {...},
   cameras: [...],
   frames: [...]
 }
```

---

# 5. Scenes (Phase 2+)

## 5.1 Scene List

### 표시 요소

- Scene 목록
    
- Scene metadata
    
- Camera count
    

```
[Scenes]
 | Scene | Cameras | Metadata |
 |-------|---------|----------|
 |Factory|   6     |  navmesh |
```

---

## 5.2 Scene Editor (별도 문서 20번에서 상세 정의)

여기서는 IA 기반 표시 요소만 요약한다.

### 표시 요소

- Floorplan view
    
- Camera 배치
    
- Crowd spawn zone
    
- Property Panel
    

```
[Scene Editor – Summary]
 [Floorplan + camera icons]
 [Right Panel: camera properties]
```

---

# 6. Workers (Phase 3+)

## 6.1 Worker Dashboard

- workerId
    
- status (Healthy / Degraded / Lost)
    
- queueRatio
    
- gpuUsage
    
- lastFrame
    

```
[Workers]
 | workerId | status | queue | gpu | lastFrame |
```

---

## 6.2 Worker Detail

- worker 상세 정보
    
- progress reports
    
- diagnostics[]
    
- failover 기록
    

```
[Worker Detail]
 workerId: w01
 Status: Healthy
 Progress: ...
 Diagnostics: ...
```

---

# 7. Robotics (Phase 4)

## 7.1 Trajectory Viewer (별도 문서에서 상세 정의)

### 표시 요소

- Floorplan + robot path overlay
    
- scrubber
    
- pose/velocity graphs
    
- drift warnings
    

```
[Trajectory Viewer]
 [Floorplan + path]
 [Scrubber]
 [Graphs]
```

---

# 8. 데이터 매핑 정리

Screen → DataModel/API 매핑 테이블을 요약한다.

|Screen|Source Data|
|---|---|
|Dashboard|/status, Session DB|
|New Session|SessionConfig, SceneMetadata|
|Session Detail – Overview|/status, performanceSummary|
|Session Detail – Cameras|FrameContext, CameraMeta, Images|
|Validation/Stats|validationSummary, statistics|
|Manifest Viewer|manifest.json|
|Scene List|SceneAssetRegistry|
|Scene Editor|SceneMetadata, CameraMeta, CrowdConfig|
|Worker Dashboard|Worker heartbeat/progress|
|Robotics Viewer|robotPose, SensorQuality|

---

# 9. 후속 문서

이 문서에서 정의한 화면은 아래 문서에서 컴포넌트·상호작용 레벨로 상세화된다.

- 19_UI_Interaction_and_Component_Guide
    
- 20_UI_Scene_Editor_And_Robot_Trajectory_View
    

---