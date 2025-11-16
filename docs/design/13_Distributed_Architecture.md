## 1. 목적

분산(distributed) 모드에서 Master(Orchestration)와 Worker(원격 Simulation/Encoding 노드) 간 상호작용을 정의한다.  
이 문서는 API Specification, System Architecture, Class Design에서 참조해야 하는 **공통 실행 모델**을 제공한다.

---

## 2. 구성 요소

| 컴포넌트 | 책임 | 관련 문서 |
|----------|------|-----------|
| Master (SessionManager + PipelineCoordinator) | 세션 생성/할당, Global ID 범위 관리, 결과 수집 | docs/design/2_System_Architecture.md |
| Worker Node | Unity Simulation + Capture/Encode 실행, Heartbeat/Progress 보고 | docs/design/11_Unity_Integration_Guide.md |
| Coordination API | `/workers/register`, `/workers/{id}/progress`, `/tasks/assign` 등 Worker ↔ Master HTTP/gRPC 인터페이스 | docs/design/5_API Specification.md |
| Diagnostics Service | Heartbeat, Retry, Failover 기록 | docs/design/3_Class_Design_Document.md |

---

## 3. Worker 라이프사이클

1. **Register**  
   - `POST /api/v1/workers/register` → `{workerId, role, capabilities(cameras, gpu), currentSession=null}`  
   - Master는 승인 시 `workerToken`과 Global ID range allocator endpoint를 반환.
2. **Heartbeat**  
   - 1초 간격 `POST /workers/{id}/heartbeat` → `{queueRatio, gpuUsage, lastFrame, status}`  
   - 3회 미수신 시 Worker를 unhealthy 처리하고 Failover 절차 진입.
3. **Task Assignment**  
   - Master가 `POST /workers/{id}/tasks`로 Scenario chunk 혹은 frame range를 전달.  
   - Worker는 `ack` 응답 후 해당 범위에 대한 Capture/Encode 수행.
4. **Progress Report**  
   - `POST /workers/{id}/progress` → `{sessionId, processedFrames, maxQueueRatio, droppedFrames, diagnostics[]}`. `/status.queueDepthSummary`는 모든 Worker progress의 maxQueueRatio 중 최대값을 사용한다.
5. **Result Submission**  
   - 완료 시 `POST /tasks/{taskId}/complete`로 manifest fragment, pose 파일, metrics snapshot 경로 전달.
6. **Unregister / Shutdown**  
   - 세션 종료 또는 Worker 장애 시 `DELETE /workers/{id}`. 미응답 시 Diagnostics가 상태를 “zombie”로 표시하고 후속 정리에 들어간다.

---

## 4. Global Person ID / Frame Range 할당

- Master는 `GlobalIdAllocator`를 통해 64-bit 범위를 `workerId`별로 예약한다.  
  - 예) Worker A: `[1, 1,000,000)`, Worker B: `[1,000,000, 2,000,000)`  
- `GlobalIdChunk`는 Task 할당 시 함께 전달되며, Worker는 자신에게 부여된 범위 내에서만 Agent를 spawn한다.  
- Frame ID는 Session 전역 단일 시퀀스이므로 Master가 `frameRange = [start, end)` 형태로 배정하고 결과 파일명/manifest에 동일 범위를 기록하게 한다.  
- Failover 발생 시 남은 Frame Range는 새로운 Worker에 재할당되며, GlobalIDChunk는 사용되지 않은 구간만 재사용한다(이미 생성된 범위는 폐기). Diagnostics 로그로 재할당 이력을 남긴다.

---

## 5. Failover / 재할당 시나리오

1. **Heartbeat Timeout**  
   - DiagnosticsService가 Worker 미응답을 감지하면 `tasks/{taskId}` 상태를 `lost`로 마크하고, output 디렉터리를 격리한다.
2. **Task Reassignment**  
   - Master는 동일 Frame Range와 새로운 GlobalIDChunk를 사용해 다른 Worker에게 재할당한다.  
   - 이미 생성된 파일이 있다면 `quarantine/`으로 이동 후 사용자 확인 필요.
3. **Global Reconciliation**  
   - Reassignment 후 ManifestBuilder는 `manifest.distributed.replay` 섹션에 어떤 Frame Range가 재생성됐는지 기록한다.

---

## 6. 문서 연계

- API Specification: 본 문서의 엔드포인트/페이로드를 `/workers/*`, `/tasks/*` 섹션에 반영한다.
- Checkpoint Mechanism: `checkpoint.distributed.workers[]` 배열에 Worker 상태(현재 frame, queueRatio, 할당 범위)를 기록해 Resume 시 동일 Worker에 다시 할당하거나 failover 절차를 이어갈 수 있도록 한다.
- Terminology: Worker/Task/GlobalIdChunk 등의 명칭은 `docs/design/common/terminology.md`에 등록한다.
