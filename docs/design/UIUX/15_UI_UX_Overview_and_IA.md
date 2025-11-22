# UI/UX Overview & Information Architecture

Forge UI/UX 체계의 상위 개념(페르소나, 주요 플로우)과 화면 구조(IA/Navigation)를 한 문서에 통합한다. 이후 화면 스펙(17), 에디터/로보틱스 상세(18, 19) 설계의 상위 구조로 작동하며, Forge 전체 설계 문서(Concept, Requirements, Architecture)와 정합성을 유지한다.

---

## 1. 목적 (Purpose)

- UX 상위 개념 정의(사용자 유형, 핵심 Use Case, User Flow)
- 전체 화면 구조(Sitemap, Navigation)와 책임 분리
- Phase별 기능 범위를 규정하고, 하위 스펙 문서(17~19)의 기반 제공
- 설계 문서(datamodel, API Spec, Robotics Extension 등)와 용어/필드 정합성 유지

---

## 2. 대상 사용자 (Personas)

### ● ML/Computer Vision 엔지니어
- Synthetic dataset 생성
- SessionConfig 관리/조정, Scene/Camera 구성 확인
- 결과 검증(Validation/Stats)

### ● 운영자(Ops)
- 장기 세션 운영/모니터링, Distributed Worker 상태 감시
- Backpressure, 에러 신호 대응

### ● 연구자(Researcher)
- 데이터 분석 및 학습 파이프라인 품질 확인
- ReID/Tracking/Occlusion 품질 검증
- Robotics sensor 데이터 분석(Phase 4)

---

## 3. 주요 Use Cases

- UC-01. 세션 생성 및 실행: Config 업로드/편집 → Scene/Camera 확인 → /session/init → /session/start
- UC-02. 런타임 모니터링: FPS/currentFrame/targetFrame, Backpressure, warnings[] 실시간 표시
- UC-03. 결과 검토: Validation Summary, Statistics, Sample Frames, manifest.json 뷰
- UC-04. Scene 관리(Phase 2+): Scene Asset Registry, 카메라 배치(Scene Editor), Crowd 설정
- UC-05. Distributed Worker 관리(Phase 3+): Worker 상태/Queue/GPU 모니터링
- UC-06. Robotics Sensor 분석(Phase 4): Trajectory Viewer, Drift/누락 프레임 분석

---

## 4. 핵심 User Flow 정의

### Flow 1: Synthetic Session 생성 → 실행 → 모니터링 → 완료 → 분석
1) Dashboard → New Session
2) SessionConfig 입력/업로드
3) Scene/Camera 구성 확인
4) Session 시작
5) Session Detail에서 진행률/Backpressure/FPS 모니터링
6) 종료 후 Validation/Statistics/Manifest 확인

### Flow 2: Scene 구성(Phase 2+)
1) Scene 목록 확인 → Scene Editor에서 카메라 배치/회전/FOV
2) Crowd 초기 영역 설정 → SessionConfig 저장

### Flow 3: 실시간 Simulation View
1) Session Detail → Cameras 탭 → 최신 Frame snapshot 확인
2) 필요 시 Auto-refresh 활성화

### Flow 4: Sample Gallery 기반 품질 검증
1) Validation/Stats 탭 → stride 기반 대표 프레임 조회
2) Occlusion/Bbox scale 등 품질 지표 시각 검토

### Flow 5: Distributed Worker 관리(Phase 3+)
1) Worker Dashboard → 상태/Queue/GPU 확인
2) Lost/Reassign 경고 대응

### Flow 6: Robotics Trajectory 분석(Phase 4)
1) Robotics → Trajectory Viewer → path overlay
2) timestamp/frame scrubber → drift/sensor 품질 점검

---

## 5. Phase별 UI 범위 정의

- Phase 1: Session 생성·관리, Dashboard, Session Detail, Validation, Manifest, Sample Frames
- Phase 2: Scene Editor(카메라 배치), Scene Asset Registry
- Phase 3: Distributed Worker 모니터링
- Phase 4: Robotics Trajectory Viewer, Sensor Drift 시각화

---

## 6. 설계 원칙 (UX Principles)

- Config 기반 구조: 스키마 기반 데이터만 노출
- 표준 용어 유지: datamodel/terminology 필드명 그대로 사용
- 관측 가능성 강조: FPS, Backpressure, warnings, metrics 시각화 우선
- 단계적 복잡도: Phase 1→4 순차 확장
- Simulation Layer 독립: UI는 편집/시각화만 담당

---

## 7. 전체 화면 구조 (Sitemap)

Forge UI는 Session 중심 → Scene/Camera 구성 → Worker 관리 → Robotics 분석의 네 도메인으로 구성된다.

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

## 8. 화면별 역할 요약

- Dashboard: /status, Session 목록, New Session 진입
- New Session: SessionConfig 생성/업로드, Scene/Camera 확인
- Session Detail: lifecycle 모니터링(Overview, Cameras, Validation/Statistics, Manifest)
- Scenes: Scene List, Scene Editor(카메라 배치, Crowd 설정)
- Workers: Worker Dashboard/Detail(Healthy/Degraded/Lost, queueRatio, gpuUsage)
- Robotics: Trajectory Viewer(trajectory overlay, scrubber, sensor graphs)

---

## 9. 화면 간 이동 규칙 (Navigation)

- Dashboard → New Session: 버튼 클릭, SessionConfig 템플릿 로드
- Dashboard → Session Detail: Session row 클릭
- Session Detail 탭: Overview ↔ Cameras ↔ Validation ↔ Manifest (상태 유지)
- Scene List → Scene Editor: Scene 선택
- Worker Dashboard → Worker Detail: row 클릭
- Robotics 메뉴 → Trajectory Viewer 진입

---

## 10. Phase별 활성화 규칙

- Phase 1: Dashboard, New Session, Session Detail 전체 활성화
- Phase 2: Scene List / Scene Editor 활성화
- Phase 3: Worker Dashboard / Worker Detail 활성화
- Phase 4: Robotics Trajectory Viewer 활성화

UI는 Phase에 따라 메뉴를 자동 숨김/비활성 처리한다.

---

## 11. IA 확장 원칙

- 새 기능은 Sitemap 하위 노드로 확장
- datamodel/API 필드명 그대로 사용
- 기능은 탭/섹션 단위로 분리하여 복잡성 최소화

---

## 12. 후속 문서

- 17_UI_Screens_and_Design_System.md (화면 스펙 + 디자인 시스템)
- 18_UI_Scene_Editor_Spec.md (Scene Editor 상세)
- 19_UI_Robotics_Trajectory_Viewer_Spec.md (Robotics Viewer 상세)
