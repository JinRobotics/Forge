# UI Screens & Design System

Forge UI의 모든 화면 스펙과 디자인 시스템(컴포넌트/인터랙션/토큰)을 통합 정리한다. 16번 문서(개요+IA)를 토대로 구체적 화면 구조, 데이터 매핑, 상호작용 규칙을 정의하며, Phase 1~4까지 확장 가능한 기준으로 사용한다.

---

## 1. 화면 설계 원칙
- 정합성 우선: datamodel, API spec, manifest, validation과 1:1 매핑
- 요약 → 상세: 상위 화면에서 요약, 클릭 시 상세
- 실시간성: /status 기반 1–3초 반영
- 관측 가능성: FPS, Backpressure, warnings, metrics 강조
- Phase 기반 확장: Scene/Workers/Robotics는 조건부 활성화

---

## 2. Dashboard

### 표시 요소
- Engine Status Panel: status, engineVersion, supportedVersions, authMode, fps, currentFrame/targetFrame, queueDepthSummary, warnings[]
- Session List Table: sessionId, status, fps, progress, qualityMode, updatedAt
- Buttons: [New Session]

### 와이어프레임
```
[Engine Status]
 Status: running   | FPS: 14.5
 Frame: 12,345 / 100,000
 Backpressure: 0.42 (OK)
 Active Scene: Factory
 Warnings:
  - QUEUE_BACKPRESSURE (capture=0.78)

[Sessions]
 ---------------------------------------------------------
 | sessionId | status | fps | progress | quality | updated |
 |-----------|--------|-----|----------|---------|---------|
 | factory01 | running|14.5 | 12%      | strict  | 10s ago |
 ---------------------------------------------------------
```

### 데이터 매핑
|UI 요소|필드/출처|단위/주기|에러 처리|
|---|---|---|---|
|status|`/status.status`|즉시|배너(error)|
|engineVersion|`/status.engineVersion`|즉시|배너(error)|
|fps|`/status.fps`|1~3초|배너(warn) + 리트라이|
|currentFrame/targetFrame|`/status.progress`|1~3초|배너(warn)|
|queueDepthSummary|`/status.queueDepthSummary`|1~3초|배너(warn)|
|warnings[]|`/status.warnings[]`|1~3초|Warning Banner|
|session 리스트|Session DB/manifest 메타데이터|5~10초|표 빈 상태 + 재시도 버튼|

---

## 3. New Session

### 표시 요소
- SessionConfig 입력: sessionId, totalFrames, qualityMode(strict/relaxed), frameRatePolicy.id
- Validation Feedback: JSON Schema 기반 실시간 검증, 오류시 붉은 테두리/툴팁
- Scene 선택, Camera 목록(CameraMeta)

### 와이어프레임
```
[New Session 레이아웃]
 -----------------------------------------------------
 | Header: New Session                                |
 |---------------------------------------------------|
 | Form (좌)       | Scene/Camera 리스트 (우)         |
 | sessionId       | Scenes: Factory, Office          |
 | totalFrames     | Cameras: cam01, cam02...         |
 | qualityMode     | Snapshot 없는 경우 placeholder   |
 | frameRatePolicy |                                   |
 | [Start Session] |                                   |
 -----------------------------------------------------

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

### 데이터 매핑
|UI 요소|필드/출처|단위/주기|에러 처리|
|---|---|---|---|
|sessionId 입력|SessionConfig.sessionId|입력 즉시|스키마 실패 시 필드 포커스 + 툴팁|
|totalFrames|SessionConfig.totalFrames|입력 즉시|최소/최대 범위 오류 시 경고 테두리|
|qualityMode|SessionConfig.qualityMode|입력 즉시|옵션 미선택 시 버튼 비활성|
|frameRatePolicy|SessionConfig.frameRatePolicy.id|입력 즉시|옵션 미선택 시 버튼 비활성|
|Scene 목록|SceneMetadata|로드 시|로딩 실패 배너 + 재시도|
|CameraMeta 리스트|CameraMeta|로드 시|로딩 실패 배너 + 재시도|

---

## 3.5 렌더링/성능 대응 (Unity)

- FrameRatePolicy: 렌더링 시간 기준치를 초과하면 자동으로 해상도 다운샘플, 카메라 샘플링 간격 확대, 프레임 스킵 순으로 적용
- Unity 최적화 체크: 배칭/오클루전/LOD 활성 여부를 릴리스 전 체크리스트로 검증
- Snapshots: 고해상도/멀티 카메라 시 우선순위 카메라만 실시간, 나머지는 샘플링 주기 증가

## 4. Session Detail

### Overview 탭
- status/quality, fps(avg), currentFrame/targetFrame
- Backpressure indicator(queueDepthSummary)
- warnings[], FPS timeline, diagnostics

### Cameras 탭 (Live Preview)
- CameraMeta 목록
- 최신 snapshot, frame_id, timestamp, person count, occlusion avg
- Auto-refresh toggle, Recent frame thumbnails

### Validation / Statistics 탭
- validationSummary: frameDropCount, poseMissingCount, syncDriftExceeded
- statistics 그래프: bbox scale histogram, occlusion histogram, detection count plot
- Sample Frames (stride 기반)

### Manifest Viewer 탭
- manifest.json 트리/표
- 주요 섹션: performanceSummary, stageStatuses, reidArtifacts, sensorArtifacts(Phase 4)

### 데이터 매핑
|탭|UI 요소|필드/출처|단위/주기|에러 처리|
|---|---|---|---|---|
|Overview|status/quality|`/status.status`, manifest.qualityMode|1~3초|배너|
|Overview|fps/currentFrame/targetFrame|`/status.fps`, `/status.progress`|1~3초|배너|
|Overview|Backpressure|`/status.queueDepthSummary`|1~3초|경고 배너 + 색상|
|Cameras|Live snapshot|`/session/{id}/camera/{id}/latest`|3초|플레이스홀더 + 재시도|
|Cameras|frame_id/timestamp|snapshot 메타데이터|3초|플레이스홀더|
|Validation|validationSummary|`validation.json`|완료 시|표시 불가 시 경고 문구|
|Statistics|histogram/plots|`statistics.json`|완료 시|그래프 비활성 + 경고|
|Manifest|manifest 트리|`manifest.json`|로드 시|JSON 파싱 실패 배너|

---

## 5. Scenes (Phase 2+)

### Scene List
```
[Scenes]
 | Scene | Cameras | Metadata |
 |-------|---------|----------|
 |Factory|   6     |  navmesh |
```

### Scene Editor (요약, 세부는 18번 문서)
```
[Scene Editor – Summary]
 [Floorplan + camera icons]
 [Right Panel: camera properties]
```

### 데이터 매핑
|UI 요소|필드/출처|단위/주기|에러 처리|
|---|---|---|---|
|Scene 목록|SceneAssetRegistry|로드 시|배너 + 재시도|
|Scene metadata|SceneMetadata|로드 시|플레이스홀더|
|Camera count|SceneMetadata.cameras|로드 시|'-' 표시|
|Floorplan|SceneMetadata.floorplan/navmesh|로드 시|플레이스홀더|
|Camera 아이콘|SessionConfig.cameras[].extrinsic|사용자 조작 즉시|스냅/범위 초과 시 경고|
|Crowd 영역|SessionConfig.crowd|사용자 조작 즉시|유효성 실패 시 롤백|

---

## 6. Workers (Phase 3+)
```
[Workers]
 | workerId | status | queue | gpu | lastFrame |
```

### 데이터 매핑
|UI 요소|필드/출처|단위/주기|에러 처리|
|---|---|---|---|
|worker 상태|Distributed heartbeat|3~5초|배너 + 상태 회색 처리|
|queueRatio/gpuUsage|worker metrics|3~5초|숫자 회색 처리 + 재시도|
|lastFrame|worker progress|3~5초|회색 처리|

---

## 7. Robotics (Phase 4)
```
[Trajectory Viewer]
 [Floorplan + path]
 [Scrubber]
 [Graphs]
```

### 데이터 매핑
|UI 요소|필드/출처|단위/주기|에러 처리|
|---|---|---|---|
|Trajectory overlay|FrameContext.sensors.robotPose|3~5초(또는 scrub)|로딩 실패 시 안내 텍스트|
|Scrubber|frameId/timestamp|사용자 조작|범위 밖 이동 차단|
|Graphs(position/yaw/velocity/drift)|robotPose, SensorQuality.sync_offset_ms|3~5초|그래프 회색 처리|
|Drift 경고|SensorQuality|3~5초|붉은 배지 + Tooltip|

---

## 8. 디자인 토큰 (Design Tokens)

|토큰|값/규칙|비고|
|---|---|---|
|Primary|#2B67F6|버튼/링크|
|Success|#1DAA3E|status: running/healthy|
|Warning|#F5A524|Backpressure CAUTION|
|Error|#E63946|중단/실패 배너|
|Info|#4C6FFF|정보 배너/아이콘|
|배경|#0F1115 / #F7F8FA|다크/라이트 대응|
|폰트|`"Inter", "Noto Sans KR", sans-serif`|영문+국문 가독성|
|타입 스케일|12 / 14 / 16 / 20 / 24px|본문=14, 라벨=12|
|간격 스케일|4 / 8 / 12 / 16 / 24px|컴포넌트 간 마진|
|아이콘 크기|16px / 20px|상태/버튼 아이콘|
|그래프 두께|1.5px 라인, 10px 바|타임라인/히스토그램|

---

## 9. 핵심 컴포넌트 & 인터랙션 규칙

### Status Badge
- 상태: idle(회색), ready(파랑), running(초록), paused(노랑), error(빨강)
- 클릭 시 상세 화면 이동

### Backpressure Indicator
- 구간: <0.7 OK(초록), 0.7~0.9 CAUTION(노랑), 0.9~1.0 SLOW(주황), ≥1.0 PAUSE(빨강)
- 사용 위치: Dashboard, Session Detail Overview, Worker Dashboard

### Warning Banner
- error=빨강/흰 글씨, warning=노랑/검정, info=회색/검정
- 화면 상단 고정

### Tabs Component
- Overview, Cameras, Validation/Statistics, Manifest
- 탭 이동 시 상태 유지, 브라우저 history 미반영

### Table Component
- 좌측 정렬, Row clickable, 상태 배지는 첫/두 번째 컬럼

### Graph Components
- Line(FPS, drift), Bar(bbox/occlusion histogram), Scatter(sensor alignment)
- Zoom 기본 제공, X=timestep/frameId, Y=metric

### Live Preview (Cameras)
- 주기: 기본 3초, Auto-refresh ON 시 지속
- 썸네일 클릭 → 확대, 우클릭 → Save Frame

### Timeline Scrubber (Robotics)
- scrub 시 floorplan overlay + sensor graphs 동기
- 기준: frameId 또는 timestamp, drift 구간 붉은색 하이라이트

### Scene Editor Interaction (요약)
- 카메라 드래그/회전/FOV 슬라이더
- Crowd spawn zone 드래그/크기 조절, density slider
- Waypoint(Phase 4): Click 추가, Drag 이동, Right-Click 삭제, Path Click 삽입

### Validation Feedback
- Trigger: onBlur 또는 500ms debounce 후 검사
- Error: Border #FF0000 + 느낌표 아이콘 + Tooltip(JSON Schema 오류)
- Success: Border 기본/녹색 복귀
- Config diff 트래킹으로 지속 편집 상태 유지

### 스냅/그리드/멀티 셀렉션 (Scene Editor & Robotics)
- 위치 스냅 0.1m 격자(토글 가능), 회전 스냅 5도(Shift로 해제)
- 멀티 셀렉션: Shift+클릭, 드래그 박스
- 이동/회전 제한: Floorplan 경계 밖 이동 차단, 충돌 시 경고
- 줌/팬: 휠 줌, Space+드래그 팬, 최소/최대 줌 0.5x~4x

---

## 10. 저장/Undo 및 에러 처리 규칙

- Undo/Redo 스택 최대 20 (Ctrl+Z / Ctrl+Shift+Z)
- 저장 실패: 에러 배너 + 실패 필드 표시, 저장 버튼 재활성화, 포커스 이동
- 검증 실패: 필드 스크롤/포커스, 테두리 강조 + Tooltip
- 자동 저장 미사용, 명시적 [Apply]/[Save] 사용
- API 실패: 에러 배너 자동
- Session error: Session Detail 상단 고정 레드 배너
- Worker Lost: Worker Dashboard에서 두 번 깜빡임 후 빨강 표시

---

## 11. 접근성 / 사용성 규칙
- 모든 컬러 인디케이터는 텍스트 레이블 포함(색약 대비)
- 중요 정보(Backpressure, warnings)는 색상+아이콘 이중 신호
- 반응형 레이아웃 기준 폭 1280px

---

## 12. 후속/연계 문서
- 18_UI_Scene_Editor_Spec.md (Scene Editor 상세)
- 19_UI_Robotics_Trajectory_Viewer_Spec.md (Robotics Viewer 상세)
