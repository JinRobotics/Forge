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

## 5.1 Worker 상태 기계(State Machine)

```
 Registered ──heartbeat OK──► Healthy ──error report──► Degraded
      │                           │                         │
      │                           └──heartbeat miss N회──► Lost
      │                                                      │
      └────manual remove◄────Quarantined◄──fail diag◄─── Lost
```

| 상태 | 조건 | Master/Diagnostics 조치 |
|------|------|------------------------|
| Registered | `/workers/register` 성공 후 task 미할당 | Capability catalog 업데이트 |
| Healthy | 최근 3회 heartbeat OK, `queueRatio < 0.85` | Normal dispatch |
| Degraded | Heartbeat는 있으나 오류 보고/queueRatio≥0.85 | Backpressure 신호, task 조정(감속) |
| Lost | Heartbeat N회(기본 3) 누락 | Task 상태 `lost`, 파일 `quarantine/worker/<id>/` 이동 |
| Quarantined | 반복 실패/보안 이슈 | 재등록 차단, 관리자 승인 필요 |

- 상태 전이는 DiagnosticsService가 결정하고 `/metrics`에 노출한다.  
- Lost → Quarantined 전환 시 해당 Worker의 GlobalIDChunk는 폐기되며, Hash 기록을 `scene_asset_audit`과 유사하게 남긴다.

---

## 5.2 Master Failover 전략

- **구성**: Active/Passive 2노드. Passive는 Master의 `checkpoint.distributed` 스냅샷을 rsync/raft 로그로 수신.  
- **Checkpoint 항목**:
  - `workers[]`: workerId, 상태, 마지막 heartbeat, currentTask, queueRatio
  - `tasks[]`: frameRange, globalIdChunk, status(`assigned/running/completed/lost`)
  - `globalIdAllocator`: nextRangeStart, chunkSize, retiredChunks
  - `manifestFragments` 경로, `metricsSnapshot`
- **Failover 흐름**:
  1. Active Master heartbeat timeout → Passive가 5초 내 리더 선출.
  2. Passive가 Latest checkpoint 로드, `epoch` 증가 후 `/workers/rebind` 이벤트 발행.
  3. Worker는 `POST /workers/rebind`로 새 Master에 등록하고 현재 상태를 보고한다.
  4. Master는 `tasks[]` 상태를 reconcile: `running`이던 task는 `lost`로 표시 후 재배정.

---

## 5.3 ID Range 롤백/재배정

- 각 Task에 부여한 `GlobalIdChunk`는 Manifest에 `chunkId`로 기록.  
- Reassignment 시:
  1. 기존 chunk는 `retiredChunks`에 추가하고 재사용하지 않는다.
  2. 새로운 chunk 발급 후 Task에 매핑, Manifest `distributed.replay[]`에 `{frameRange, retiredChunkId, newChunkId}` 기록.
  3. StorageWorker는 `retiredChunkId`에 해당하는 파일을 `quarantine/dropped_chunks/<chunkId>`로 이동해 중복 Export를 방지한다.

- 롤백 필요 시(예: Master 오류로 잘못된 chunk 부여):
  - Diagnostics가 `chunkIntegrity` 경고를 띄우고, Admin 명령으로 대상 chunk를 폐기 후 재할당한다.

---

## 5.4 네트워크 장애 및 재분배 정책

| 시나리오 | 감지 방법 | 대응 |
|----------|----------|------|
| 일시적 패킷 손실 | Heartbeat RTT spike, `queueRatio` 정상 | `Degraded` 상태 전환 후 30초 모니터링, 자동 복구 |
| 지속적 지연 | Heartbeat RTT > 500ms, `maxQueueRatio` 상승 | Task를 줄여 다른 Worker에 분산, Streaming Gateway FPS 조정 |
| 연결 단절 | Heartbeat 연속 3회 실패 | 상태 `Lost`, 재할당 프로세스 실행 |

- 네트워크 지연 시 Master는 `BackpressureLevel`을 Worker별로 계산하고, `GenerationController`에 전달해 프레임 생성 속도를 조절한다.
- gRPC/HTTP 재시도 정책:
  - Heartbeat: 1초 간격, 5회 시도 후 `Lost`
  - Task 배포: exponential backoff (1s, 2s, 4s), 실패 시 다른 Worker 선택

---

## 6. 문서 연계

- API Specification: 본 문서의 엔드포인트/페이로드를 `/workers/*`, `/tasks/*` 섹션에 반영한다.
- Checkpoint Mechanism: `checkpoint.distributed.workers[]` 배열에 Worker 상태(현재 frame, queueRatio, 할당 범위)를 기록해 Resume 시 동일 Worker에 다시 할당하거나 failover 절차를 이어갈 수 있도록 한다.
- Terminology: Worker/Task/GlobalIdChunk 등의 명칭은 `docs/design/common/terminology.md`에 등록한다.

---

## 7. Manifest Fragment Merge

분산 모드에서는 Worker별 manifest fragment가 생성되고 Master가 병합한다.

### 7.1 출력 구조
```
output/session_xxx/meta/
 ├── manifest_fragment_workerA.json
 ├── manifest_fragment_workerB.json
 └── manifest.json (최종)
```

각 fragment에는 다음 필드가 포함된다:
- `frameRange`: `[startFrame, endFrame)`
- `globalIdChunkId`
- `cameras[]`, `statistics`, `validationSummary`

### 7.2 병합 알고리즘
1. Master는 fragment를 frameRange 기준으로 정렬한다.
2. 겹치는 frameRange가 감지되면 우선순위 규칙을 적용한다:
   - `replay` 재생성 구간이 존재하면 최신 fragment 우선.
   - 동일 시간에 두 fragment가 존재하면 `globalIdChunkId`가 더 큰(새로운) fragment 우선.
3. 병합 과정에서 제외된 fragment는 `manifest.distributed.replay[]`에 `{frameRange, retiredChunkId}`로 기록한다.

### 7.3 충돌 처리

| 충돌 유형 | 판단 기준 | 해결 정책 |
|-----------|-----------|-----------|
| Frame 중복 | 동일 `frame_id`가 두 fragment에 존재 | 최신 fragment 유지, 이전 fragment 기록은 `replayDrops[]`에 추가 |
| Timestamp 역전 | fragment 정렬 후 `timestamp`가 감소 | 경고 로그 + fragment reorder. 수정 불가 시 해당 구간 skip |
| Statistics 합산 | detection/person count 중복 | frameRange 기준 재계산 후 합산. Manifest병합기에서 `statistics` 값을 다시 계산 |

### 7.4 최종 Manifest 생성
```
finalManifest = MergeFragments(fragments)
finalManifest.distributed = {
  "workers": [...],
  "replay": [...],
  "fragmentHashes": [...]
}
```

- 병합 후 각 fragment의 SHA256을 기록해 재현성을 검증한다.
- ManifestWriter는 병합 로그를 `meta/manifest_merge.log`로 남긴다.
