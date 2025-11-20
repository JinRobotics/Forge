
Forge UI 전체의 "화면 구조(IA)"와 "탐색 구조(Navigation)"를 정의하는 문서로, 모든 화면을 계층적으로 정리하고 각 화면이 어떤 기능/데이터를 책임지는지 명확하게 구분한다.  
이 문서는 16_UI_UX_Overview_and_Flows의 상위 개념을 구체적인 화면 구조로 변환한 단계이다.

---

# 1. 목적 (Purpose)

- Forge UI의 전체 화면 체계를 정의한다.
    
- 사용자 플로우에 따라 어떤 화면이 필요하고, 어떻게 이동하는지 명확히 표현한다.
    
- 이후 18번 문서(Screen Spec)에서 와이어프레임/세부 레이아웃이 이어질 수 있도록 구조적 기반을 제공한다.
    
- 기존 시스템 설계 문서(Architecture, Pipeline, API Spec, Robotics Extension 등)의 기능을 UI 구조에 맵핑한다.
    

---

# 2. 전체 화면 구조 (Sitemap)

Forge UI는 크게 **Session 중심**, **Scene/Camera 구성**, **Worker 관리**, **Robotics 분석** 네 가지 도메인으로 구성된다.

```
Dashboard
 ├── New Session
 └── Session Detail
       ├── Overview
       ├── Cameras (Live Preview)
       ├── Validation / Statistics
       └── Manifest Viewer

Scenes (Phase 2+)
 ├── Scene List
 └── Scene Editor (Camera Layout / Crowd Config)

Workers (Phase 3+)
 ├── Worker Dashboard
 └── Worker Detail

Robotics (Phase 4)
 └── Trajectory Viewer
```

---

# 3. 화면별 역할 정의

## 3.1 Dashboard

**역할:**

- 현재 엔진 상태(/status) 조회
    
- 전체 Session 목록 조회
    
- 새 Session 생성으로 진입
    

**관련 데이터:**

- /status API
    
- Session DB / Manifest metadata
    

---

## 3.2 New Session

**역할:**

- 새로운 SessionConfig 생성 또는 기존 파일 업로드
    
- Scene/Camera 구성 요소 확인
    

**관련 데이터:**

- SessionConfig 스키마
    
- Scene 목록(SceneMetadata)
    
- CameraMeta 리스트
    

---

## 3.3 Session Detail

Session의 전 lifecycle을 UI에서 관찰하는 핵심 화면.

### ● Overview

- FPS / currentFrame / targetFrame
    
- Backpressure, warnings[]
    
- performanceSummary 요약
    

### ● Cameras (Live Preview)

- CameraMeta 기반 카메라 목록
    
- 최신 snapshot 이미지
    
- frame_id / timestamp 표시
    

### ● Validation / Statistics

- validationSummary / statistics
    
- bbox scale histogram, occlusion histogram
    
- sample frames
    

### ● Manifest Viewer

- manifest.json의 주요 구조 시각화
    
- performanceSummary, stageStatus 등 정리된 목록
    

**관련 데이터:**

- /status API
    
- manifest.json
    
- output/images/…
    
- validation, statistics 결과
    

---

## 3.4 Scenes (Phase 2+)

### Scene List

- Scene Pool 조회
    
- Scene Metadata 확인
    

### Scene Editor

- Camera 배치(위치/회전/FOV)
    
- Crowd 초기 설정
    
- SceneMetadata 기반 floorplan 표시
    

**관련 데이터:**

- SceneAssetRegistry
    
- CameraMeta
    
- CrowdConfig
    
- SessionConfig.scenes[]
    

---

## 3.5 Workers (Phase 3+)

### Worker Dashboard

- Worker 상태(Healthy, Degraded, Lost)
    
- queueRatio, gpuUsage, lastFrame
    

### Worker Detail

- 단일 Worker의 진행 현황
    
- 재할당 이력, diagnostics 표시
    

**관련 데이터:**

- Distributed Architecture 문서의 worker heartbeat/progress
    

---

## 3.6 Robotics (Phase 4)

### Trajectory Viewer

- robotPose trajectory
    
- Floorplan 기반 경로 overlay
    
- sensor drift 표시(sensor_sync_offset_ms)
    

**관련 데이터:**

- FrameContext.sensors
    
- SensorQuality
    
- Robotics Extension 내 TUM/KITTI export
    

---

# 4. 화면 간 이동 규칙 (Navigation Rules)

### Dashboard → New Session

- 버튼 클릭 시
    
- SessionConfig 초기 템플릿 로드
    

### Dashboard → Session Detail

- Session 테이블 row 클릭 시
    

### Session Detail 내 탭 이동

- Overview ↔ Cameras ↔ Validation ↔ Manifest
    
- 상태 유지
    

### Scene List → Scene Editor

- Scene 선택 시
    

### Worker Dashboard → Worker Detail

- Worker row 클릭 시
    

### Robotics → Trajectory Viewer

- Robotics 전용 메뉴에서 진입
    

---

# 5. Phase별 활성화 규칙

- Phase 1: Dashboard, New Session, Session Detail 전체 기능 활성화
    
- Phase 2: Scene List / Scene Editor 활성화
    
- Phase 3: Worker Dashboard / Worker Detail 활성화
    
- Phase 4: Robotics Trajectory Viewer 활성화
    

UI는 Phase에 따라 메뉴를 자동으로 숨기거나 비활성화한다.

---

# 6. IA 확장 원칙

- 새로운 기능 추가 시 반드시 Sitemap 하위 노드로 확장
    
- 기존 설계문서(DataModel, API Spec, Architecture) 필드명 그대로 사용
    
- 기능은 탭/섹션 단위로 분리하여 복잡성 최소화
    

---

# 7. 후속 문서

이 문서는 다음 문서의 기반 구조를 제공한다:

- 18_UI_Screen_Specifications
    
- 19_UI_Interaction_and_Component_Guide
    
- 20_UI_Scene_Editor_And_Robot_Trajectory_View (확장 스펙)
    

---