# Phase 3 계획 (Distributed Workers & Edge Export)

## 목표
분산 Worker로 생성/처리 병렬화, Edge-friendly 포맷 실시간/배치 export.

## 범위
- Distributed Architecture: Master ↔ Worker heartbeat, queueRatio/gpuUsage 모니터링, Lost/Reassign 처리
- Backpressure 제어: PipelineCoordinator가 queueDepthSummary 기반 속도 조정
- Edge Export Worker: TFLite/ONNX 등 포맷 export, manifest.edgeArtifacts[] 기록
- Worker Dashboard: 상태/queue/gpuUsage/lastFrame 표시, Lost 경고/재시도
- 안정성: 체크포인트/재시작 시 Worker 재배치, Diagnostics 이벤트 로깅

## 해야 할 일
1) Worker 노드 실행/등록/heartbeat, queueRatio/gpuUsage 수집
2) Backpressure 제어 신호를 GenerationController에 전달(속도/프레임 스킵)
3) Edge Export 포맷(TFLite/ONNX/커스텀) 생성 및 manifest edgeArtifacts 기록
4) Worker Dashboard에서 상태/큐/경고 실시간 반영
5) 체크포인트/재시작 시 Worker 재배치, Diagnostics 이벤트 기록/알림

## 완료 기준
- Worker n≥3 환경에서 세션 실행, queueRatio 안정 <0.8, Lost 발생 시 재시도/재할당 동작
- Edge 포맷 export 생성 및 manifest에 기록, 실패 시 다른 포맷 영향 없음
- 대시보드에서 Worker 상태/큐/경고가 실시간 반영
