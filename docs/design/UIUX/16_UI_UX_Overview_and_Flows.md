
## 1. 목적 (Purpose)

Forge UI/UX 체계의 상위 개념을 정의하는 문서로, 사용자 유형(Persona), 주요 사용 시나리오, 핵심 User Flow를 규정한다. 이 문서는 이후의 IA(Information Architecture), Screen Specification, Component Guide 문서들의 상위 구조로 작동하며, Forge 전체 설계 문서(Concept, Requirements, Architecture)와 정합성을 유지한다.

UI/UX의 목적은 다음과 같다:

- Session 생성/실행/모니터링/결과 분석 과정의 전반적인 사용자 경험 정의
    
- Scene/Camera/Worker/Robotics 기능을 단계별 Phase로 UX 관점에서 구조화
    
- 시스템 복잡도를 UI 계층에서 단순화해 사용성을 보장
    

---

## 2. 대상 사용자 (Personas)

### ● ML/Computer Vision 엔지니어

- Synthetic dataset 생성
    
- SessionConfig 관리 및 조정
    
- Camera/Scene 구성 확인
    
- 결과 검증 (Validation/Stats)
    

### ● 운영자(Ops)

- 장기 세션 운영 및 모니터링
    
- Distributed Worker 상태 감시
    
- Backpressure, 에러 신호 대응
    

### ● 연구자(Researcher)

- 데이터 분석 및 학습 파이프라인 품질 확인
    
- ReID/Tracking/Occlusion 품질 검증
    
- Robotics sensor 데이터 분석(Phase 4)
    

---

## 3. 주요 Use Cases

### UC-01. 세션 생성 및 실행

- Config 파일 업로드 또는 UI 기반 편집
    
- Scene/Camera 구성 확인 및 저장
    
- /session/init → /session/start
    

### UC-02. 런타임 모니터링

- FPS, currentFrame, targetFrame
    
- Backpressure(OK/CAUTION/SLOW/PAUSE)
    
- warnings[] 실시간 표시
    

### UC-03. 결과 검토

- Validation Summary
    
- Statistics 그래프
    
- Sample Frames(Stride 기반)
    
- manifest.json 정리된 뷰
    

### UC-04. Scene 관리(Phase 2+)

- Scene Asset Registry
    
- 카메라 배치(Scene Editor)
    
- Crowd 설정
    

### UC-05. Distributed Worker 관리(Phase 3+)

- Worker 상태(Healthy/Degraded/Lost)
    
- Queue/GPU 사용량 모니터링
    

### UC-06. Robotics Sensor 분석(Phase 4)

- Trajectory Viewer
    
- Sensor Drift 및 누락 프레임 분석
    

---

## 4. 핵심 User Flow 정의

### Flow 1: Synthetic Session 생성 → 실행 → 모니터링 → 완료 → 분석

1. Dashboard → New Session
    
2. SessionConfig 입력 또는 파일 업로드
    
3. Scene/Camera 구성 확인
    
4. Session 시작
    
5. Session Detail 화면에서 진행률/Backpressure/FPS 모니터링
    
6. 종료 후 Validation/Statistics/Manifest 확인
    

### Flow 2: Scene 구성(Phase 2+)

1. Scene 목록 확인
    
2. Scene Editor에서 카메라 배치/회전/FOV 정의
    
3. Crowd 초기 영역 설정
    
4. SessionConfig에 저장
    

### Flow 3: 실시간 Simulation View

1. Session Detail → Cameras 탭
    
2. 최신 Frame JPEG snapshot 확인
    
3. 필요 시 Auto-refresh 활성화
    

### Flow 4: Sample Gallery 기반 품질 검증

1. Validation/Stats 탭 열기
    
2. select_sample_frames 로직 기반 대표 프레임 조회
    
3. Occlusion/Bbox scale 등 품질 지표 시각적으로 검토
    

### Flow 5: Distributed Worker 관리(Phase 3+)

1. Worker Dashboard 이동
    
2. Worker 상태/Queue 사용량/GPU 사용량 확인
    
3. Lost/Reassign 발생 시 경고 표시
    

### Flow 6: Robotics Trajectory 분석(Phase 4)

1. Robotics → Trajectory Viewer
    
2. 로봇 path overlay 확인
    
3. timestamp/frame scrubber 이동
    
4. drift/sensor quality 점검
    

---

## 5. Phase별 UI 범위 정의

### Phase 1

- Session 생성·관리 UI
    
- Dashboard / Session Detail / Validation / Manifest
    
- Sample Frames
    

### Phase 2

- Scene Editor(카메라 배치)
    
- Scene Asset Registry
    

### Phase 3

- Distributed Worker 모니터링
    

### Phase 4

- Robotics Trajectory Viewer
    
- Sensor Drift 시각화
    

---

## 6. 설계 원칙 (UX Principles)

- **Config 기반 구조**: UI의 모든 데이터는 기존 설계문서에서 정의된 스키마 기반으로 구성한다.
    
- **표준 용어 유지**: datamodel/terminology의 필드명 그대로 사용.
    
- **관측 가능성(Observability)** 강조: FPS, Backpressure, warnings, metrics 등 시각화 우선.
    
- **단계적 복잡도 증가**: Phase 1→4 순서로 기능 확장.
    
- **Simulation Layer 독립성**: UI는 편집/시각화만 담당하며, 시뮬레이션 physics/logic은 관여하지 않는다.
    

---

## 7. 후속 문서 연계

이 문서는 아래 문서들의 상위 개념 문서다:

- 17_UI_Information_Architecture
    
- 18_UI_Screen_Specifications
    
- 19_UI_Interaction_and_Component_Guide
    
- 20_UI_Scene_Editor_And_Robot_Trajectory_View (확장)
    

---