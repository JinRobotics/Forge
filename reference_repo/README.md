# Reference Repository Guide

Forge 프로젝트에서 참고하는 외부 리소스/분석 자료는 `reference_repo/` 최상위 디렉터리에 정리한다. 현재 구성은 다음과 같다.

| 디렉터리                    | 용도                                   | 비고                              |
| ----------------------- | ------------------------------------ | ------------------------------- |
| `com.unity.perception/` | Unity Perception 패키지 레퍼런스 소스         | Git submodule 또는 ZIP 추출본을 배치한다. |
| `SynthDet/`             | Unity Perception 기반 SynthDet 샘플 프로젝트 | 씬/Randomizer 구성 참고용.            |
| `Unity-Robotics-Hub/`   | ROS 통합 예제 및 로봇 센서 파이프라인              | 이동형 CCTV/ROS 연계를 위한 예제.         |

## 사용 지침

1. **소스 동기화**: 외부 레포를 그대로 복사하거나 submodule로 추가하되, 버전 정보(커밋 SHA)를 `REFERENCELOG.md` 등에 기록한다.
2. **분석 메모**: 별도 노트가 필요하면 각 디렉터리 내부에 `NOTES.md` 한 파일로 정리하고, 중복 문서는 생성하지 않는다.
3. **업데이트 정책**: 외부 레포를 갱신할 때는 프로젝트 구현에 영향이 있는 변경 사항만 정리하여 공유한다.
4. **용량 관리**: 대용량 자산(씬, 모델 등)은 가능하면 Git LFS 또는 별도 아카이브에 두고, 여기에는 다운로드 스크립트/경로만 남긴다.

## 참고 문서
- Unity Perception 공식 문서: `<repo-root>/reference_repo/com.unity.perception/Documentation~/`
- SynthDet 튜토리얼: `<repo-root>/reference_repo/SynthDet/docs/`
- Unity Robotics Hub 가이드: `<repo-root>/reference_repo/Unity-Robotics-Hub/tutorials/`

필요 시 이 README만 갱신하여 어떤 레퍼런스를 두고 있는지 공유하도록 한다.
