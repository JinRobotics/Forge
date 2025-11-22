# Test & Performance Strategy

테스트 전략(기능/회귀/검증)과 성능 벤치마크 전략을 통합 정리한다. 시스템 요구사항(1), 아키텍처(2), 데이터 파이프라인(4)과 정합성을 유지하며, API/Database/Distributed/Robotics 등 하위 영역의 검증 기준을 포함한다.

---

## 1. 목표
- 기능/품질 회귀 방지, 성능 목표(fps, latency, backpressure) 달성 여부 검증
- Synthetic Session 전주기(생성→모니터링→결과)를 자동화 테스트로 커버
- Phase 확장(Scene/Worker/Robotics) 시 성능 기준을 선제 정의

---

## 2. 테스트 범주
- Unit: 핵심 로직, 스키마 검증, 변환 함수
- Integration: API 엔드포인트(/session, /status, /manifest, /validation), DB 쿼리, 파이프라인 연동
- E2E: Session 생성→실행→완료→결과 검증 플로우
- Regression: 버그 이력 기반 케이스 재실행
- Chaos/Failure Injection: Worker Lost, Queue backpressure, Network 지연

---

## 3. 테스트 케이스 핵심 영역
- Session Lifecycle: init/start/stop, 상태 전이, 오류 배너 노출
- Config Validation: SessionConfig 스키마, Camera/Scene 필드 유효성
- Output 검증: manifest.json, validation.json, statistics.json 구조/필드/단위
- Distributed: Worker heartbeat/queue/gpuUsage, 재할당 시나리오
- Scene/Camera: extrinsic/intrinsic 매핑, Snapshot 수집, Crowd 설정 반영
- Robotics(Phase 4): robotPose, SensorQuality, drift 경고

---

## 4. 성능 목표(메트릭)
- FPS: targetFrame 대비 실측 fps 오차 < 10% (품질 모드별 허용)
- Backpressure(queueDepthSummary): < 0.7 OK, 0.7~0.9 CAUTION, ≥0.9 SLOW/PAUSE
- Latency: /status API < 200ms, snapshot fetch < 500ms (로컬 기준)
- Worker: queueRatio 안정 < 0.8, gpuUsage 70~90% 권장, Lost 없음
- Robotics: sync_offset_ms 절대값 < 5ms, 누락 프레임 비율 < 0.5%

---

## 5. 벤치마크 시나리오
- Baseline: 단일 Session, 기본 Scene/Camera 구성, targetFrame 100k
- Stress: 다중 Session 동시 실행, Camera 해상도/개수 증가
- Distributed: Worker n≥3, queueRatio/gpuUsage 분산 확인
- Scene Heavy: 복잡한 SceneMetadata, Crowd 밀도↑, Camera FOV 다양화
- Robotics: 긴 trajectory(>10min), scrub 동작, drift 경고 응답

---

## 6. 데이터/로그 수집
- /status 주기 1~3초 스냅샷, fps/queueDepthSummary/warnings 기록
- manifest.json, validation.json, statistics.json 아티팩트 보관
- Worker metrics(heartbeat, queueRatio, gpuUsage) 타임라인 수집
- Robotics: robotPose, SensorQuality(sync_offset_ms) 시계열 저장

---

## 7. 합격 기준 (Pass/Fail)
- 기능: 필수 API 200 OK, 스키마 검증 통과, UI 경고/배너 적절 노출
- 성능: 섹션 4의 목표 충족, 벤치마크 시나리오 내 Backpressure 경고 ≤ CAUTION 유지
- 안정성: 크래시/데드락/메모리 누수 없음, Worker Lost 없음(Chaos 제외)

---

## 8. 자동화/도구
- 스키마 검증: JSON Schema 검증 스텝
- API 테스트: HTTP 테스트 스위트(/session, /status, /manifest 등)
- 성능 측정: /status poller + 로그 집계 스크립트, 이미지 fetch 타이머
- 리포트: 테스트 결과 + 메트릭 테이블을 CI 아티팩트로 남김
- CI 커버리지: MockSimulationGateway로 Orchestration/Pipeline 로직을 Unity 없이 100% 실행, Unity 의존 시나리오는 분리 실행
- 렌더링 초과 대응 테스트: FrameRatePolicy에서 렌더링 시간 초과 시 다운샘플/카메라 샘플링 간격 조정 동작 검증

---

## 9. 회귀 관리
- 버그 티켓별 재현 테스트를 회귀 스위트에 고정 편성
- 주요 릴리스 전 E2E + 성능 벤치마크 전체 실행
- Phase 기능 추가 시 해당 영역 회귀(예: Scene Editor/Robotics 관련 케이스) 포함

---

## 10. Phase별 포커스
- Phase 1: Session lifecycle, manifest/validation/statistics, Backpressure
- Phase 2: Scene Editor/SceneMetadata/Crowd 반영 테스트, Snapshot 품질
- Phase 3: Distributed Worker 상태, queue/gpuUsage 밸런스, Lost/재할당
- Phase 4: Robotics trajectory, drift 경고, scrub 성능

---

## 11. 참고 문서
- 1_System_Requirements.md
- 2_System_Architecture.md
- 4_Data_Pipeline_Specification.md
- 5_API Specification.md
- 6_Database_Schema.md
 - 8_Checkpoint_Mechanism.md
- 13_Distributed_Architecture.md
- 18_UI_Scene_Editor_Spec.md
- 19_UI_Robotics_Trajectory_Viewer_Spec.md

---

## 12. 벤치마크 계획 (I/O/렌더링)
- 스토리지: StorageWorker 단일/다중 구성, SSD 캐시/버퍼링 여부, JPG/PNG 인코딩 품질별 쓰기 TPS 측정
- 디스크 병목 감지: queueDepthSummary와 StorageWorker 처리량 상관관계 기록
- 렌더링: 해상도/카메라 수 증가 시 렌더링 시간 측정, FrameRatePolicy 기준치 초과 시 다운샘플/프레임 스킵/카메라 샘플링 정책 검증
