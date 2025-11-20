
Forge UI의 모든 인터페이스 요소(Component)와 상호작용 규칙(Interaction Rule)을 표준화하는 문서이다.

16번(UX 개요), 17번(IA), 18번(Screen Spec)이 "무엇을 보여줄지"를 정의했다면,  
이 문서는 "어떻게 보여주고 어떻게 동작해야 하는지"를 정의한다.

Forge 전체 UI의 일관성, 사용성, 가독성을 유지하기 위한 기준으로 기능 추가 시 반드시 참조해야 한다.

---

# 1. 목적 (Purpose)

- UI 전반에서 재사용되는 컴포넌트(Component)의 규격 정의
    
- 상태/경고/Backpressure 등을 일관된 디자인 언어로 표현
    
- 상호작용(Interaction) 패턴 규정
    
- Phase 1~4에 걸쳐 확장 가능한 UI 컴포넌트 체계 구축
    

---

# 2. 공통 컴포넌트 (Core Components)

## 2.1 Status Badge

Session, Worker, Scene의 상태를 표준화된 색상/문구로 표시한다.

|상태|색상|설명|
|---|---|---|
|idle|회색|세션 비활성 상태|
|ready|파랑|초기화 완료|
|running|초록|세션 정상 진행 중|
|paused|노랑|Backpressure 또는 사용자 pause|
|error|빨강|오류 발생|

### 상호작용 규칙

- 클릭 시 해당 객체(Session/Worker) 상세 화면으로 이동
    

---

## 2.2 Backpressure Indicator

queueDepthSummary 기반으로 시스템 부하를 시각화한다.

|구간|수준|색상|설명|
|---|---|---|---|
|< 0.7|OK|초록|정상|
|0.7 ~ 0.9|CAUTION|노랑|주의 필요|
|0.9 ~ 1.0|SLOW|주황|속도 저하|
|≥ 1.0|PAUSE|빨강|프레임 생성 중단|

### 사용 위치

- Dashboard 상단
    
- Session Detail → Overview 탭
    
- Worker Dashboard
    

---

## 2.3 Warning Banner

warnings[] 또는 diagnostics에서 제공되는 메시지를 강조한다.

### 규칙

- 화면 최상단 고정 배너
    
- error-level: 빨간 배경 + 흰 글씨
    
- warning-level: 노란 배경 + 검은 글씨
    
- info-level: 회색 배경 + 검은 글씨
    

---

## 2.4 Tabs Component

Session Detail 화면에서 사용되는 탭 구조.

### 탭 목록

- Overview
    
- Cameras
    
- Validation/Statistics
    
- Manifest
    

### 규칙

- 탭 변경 시 상태(state) 유지
    
- 탭 이동은 브라우저 history에 반영하지 않음(단일 페이지 내 이동)
    

---

## 2.5 Table Component

세션 목록, Scene 목록, Worker 목록 등 반복되는 표 형태 UI.

### 규격

- 좌측 정렬
    
- 행 클릭 가능(Row clickable)
    
- 상태 badge는 항상 첫 번째 또는 두 번째 컬럼에 배치
    

---

## 2.6 Graph Components

time-series 기반 지표(FPS, detection count, drift 등)를 표현.

### 종류

- Line graph: FPS timeline, sync drift
    
- Bar graph: bbox histogram, occlusion histogram
    
- Scatter: sensor timestamp alignment
    

### 규칙

- Zoom 기능 기본 제공
    
- X축: timestamp or frameId
    
- Y축: metric-specific
    

---

# 3. 상호작용 규칙 (Interaction Patterns)

## 3.1 Live Preview

Cameras 탭에서 프레임 스냅샷을 표시하는 규칙.

### 주기

- 기본 3초
    
- Auto-refresh ON일 경우 지속 업데이트
    

### 사용자 조작

- 썸네일 클릭 → 해당 프레임 크게 표시
    
- 이미지 우클릭 → Save Frame
    

---

## 3.2 Timeline Scrubber (Robotics)

Trajectory Viewer UI의 핵심 상호작용.

### 규칙

- scrubber 이동 시 floorplan overlay + sensor graphs 동시에 업데이트
    
- scrubber는 frameId 또는 timestamp 기준
    
- drift 경고 구간은 붉은색으로 하이라이트
    

---

## 3.3 Scene Editor Interaction

### Camera

- 카메라 아이콘 드래그 → extrinsic.position 변경
    
- 회전 핸들 드래그 → extrinsic.rotation 변경
    
- FOV 슬라이더 조정 → fov 변경
    

### Crowd

- spawn zone 박스 크기 조절 가능
    
- density slider 적용
    

지속적인 편집 작업은 Config diff로 관리될 수 있게 UI 내부적으로 변경사항 트래킹을 유지한다.

---

# 4. Phase별 컴포넌트 활성화 규칙

|Phase|활성 컴포넌트|
|---|---|
|Phase 1|Status Badge, Backpressure Indicator, Session Detail Tabs, Graphs|
|Phase 2|Scene Editor Components(FOV cone, camera drag)|
|Phase 3|Worker 상태 컴포넌트(health indicators)|
|Phase 4|Robotics Scrubber, Trajectory 그래프, SensorQuality badges|

---

# 5. 에러/비정상 상태 처리 규칙

- API 실패 → 에러 배너 자동 표시
    
- Session error → Session Detail 상단에 고정 레드 배너
    
- Worker Lost → Worker Dashboard에서 깜빡임 효과(두 번) 후 빨강 표시
    

---

# 6. 접근성 / 사용성 규칙

- 모든 컬러 인디케이터는 텍스트 레이블을 포함해야 함(색약 대비 대응)
    
- 중요 정보(Backpressure, warnings)는 2가지 이상의 시각적 신호 사용(색상+아이콘)
    
- 반응형 레이아웃: 좌우 1280px 기준
    

---

# 7. 후속 문서

이 문서 후에는 다음 문서로 이어진다:

- 20_UI_Scene_Editor_And_Robot_Trajectory_View (Scene/Robotics 상세)
    
- 21_UI_Technical_Implementation_Guide (선택: 프런트 구현 가이드)
    

---