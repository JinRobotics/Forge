# Phase 1 계획 (Core Loop & Minimal Dashboard)

## 목표
단일 노드에서 세션 생성 → 프레임 생성/저장 → 상태 모니터링까지 동작하는 최소 엔드투엔드 구현.

## 범위
- 세션/Config 처리: sceneName, cameras(intrinsic/extrinsic/fov/resolution), qualityMode, targetFrames
- API: `/session/init`, `/session/start`, `/session/stop`, `/status` (fps/currentFrame/totalFrames/progress/backpressure/warnings)
- Scene/Camera 초기화: Scene 로드, PerceptionCamera 생성/설정, Randomizer 파라미터 적용
- 파이프라인 스텁: Capture → Annotation(GT 기반 bbox) → Encode(JPG) → Storage(세션 디렉토리)
- 대시보드: `/api/status` 폴링(1~3s), 상태/프레임/배압/세션 진행률 표시
- 검증: 최소 100k 프레임 실행(OOM 없이), `/status` 값 변화 확인

## 해야 할 일
1) SessionConfig 확장 및 JSON 검증 추가
2) SceneManager/Camera 초기화 연결(PerceptionCamera 생성/설정)
3) Capture/Annotation/Encode/Storage 스텁 구현
4) Backpressure 계산(큐 길이 기반) 및 `/status` 반영
5) 대시보드에서 `/api/status` 필드 모두 표시, 실패 시 경고
6) smoke 테스트: Unity 플레이 → curl init/start → 이미지/라벨 저장 확인

## 완료 기준
- `/session/init`→`/session/start` 호출 시 프레임 생성/저장, `/status`에 fps/progress/backpressure가 변하고 대시보드에 반영
- 최소 100k 프레임 실행(OOM 없음)
