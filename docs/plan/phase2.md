# Phase 2 계획 (Scene Editor & Advanced Labels)

## 목표
Scene/Camera 배치, Crowd 설정, Occlusion/Visibility 등 고급 라벨을 포함한 데이터 생성.

## 범위
- Scene Editor: 카메라 배치/회전/FOV 조정, Crowd spawn/density 설정, Undo/Redo, 위치/회전 스냅(0.1m/5도)
- Scene Asset Registry: 사용자 정의 Scene 등록/검증/목록 조회
- Randomizer 확장: 조명/색감/노이즈/군중 밀도, 카메라 이동(waypoints)
- 라벨 확장: occlusion/visibility 계산, ReID crop export 옵션
- Manifest/Validation: validation.json, statistics.json 생성, manifest에 occlusion/visibility/scene 메타 포함
- UI: Session Detail(Cameras snapshot, Validation/Statistics, Manifest 요약)

## 해야 할 일
1) Scene Editor 기능 확장 및 Config 반영(Undo/Redo/스냅)
2) Scene Asset 등록/검증 API + UI 연동
3) Randomizer 추가/확장(조명/노이즈/군중/waypoints)
4) Occlusion/Visibility 라벨 계산 + ReID crop export
5) Validation/Statistics/Manifest 생성, Session Detail 뷰 반영

## 완료 기준
- Scene Editor에서 편집한 카메라/군중 설정이 SessionConfig에 반영되고 결과에 적용
- occlusion/visibility 필드가 라벨·manifest에 포함, ReID crop export 동작
- Scene 등록/목록/선택이 UI와 생성 파이프라인에 반영
