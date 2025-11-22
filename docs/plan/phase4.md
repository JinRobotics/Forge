# Phase 4 계획 (Robotics & Multi-Sim Sync)

## 목표
로봇 센서/궤적 시각화 및 Isaac Sim 등과의 동기화, Trajectory Viewer 제공.

## 범위
- MultiSimSyncCoordinator: Unity/Isaac lock-step 동기화(simulationTick+timestamp), 드리프트 한계/재동기화 정책 구현
- Robotics 데이터: robotPose/SensorQuality 수집, sync_offset_ms 계산, drift 경고
- Trajectory Viewer: floorplan+path overlay, scrubber, sensor graphs, drift 하이라이트
- 데이터 export: TUM/KITTI 포맷 지원, manifest.sensorArtifacts[] 기록

## 해야 할 일
1) MultiSimSyncCoordinator 구현 및 simulationTick/timestamp 동기화
2) Robotics 데이터 수집/metrics 계산(sync_offset_ms, 누락 프레임 감지)
3) Trajectory Viewer UI/모듈 구현(overlay+scrubber+그래프 동기 갱신)
4) TUM/KITTI export 기능 추가, manifest에 경로/버전 기록

## 완료 기준
- Unity+Isaac 동시 실행 시 FrameContext에 simulationTick/timestamp 포함, 드리프트 임계치 초과 시 경고
- Trajectory Viewer에서 scrubber 이동 시 overlay/그래프 동기 갱신
- TUM/KITTI export 파일 생성, manifest에 기록
