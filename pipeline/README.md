# Pipeline Assets

이 디렉터리는 실행 세션과 파이프라인 튜닝에 필요한 **비코드 산출물**을 관리한다.

## 구조

- `configs/`
  - 세션 실행용 JSON 설정 예시 (카메라/Scene/랜덤화 옵션).
  - `session_example_factory.json`: 단일 Scene 기본 세션 템플릿.
  - `session_multiscene_phase2.json`: Phase2 도메인 랜덤화 + 멀티 Scene 예시.
- `schemas/`
  - Config 및 manifest 관련 JSON Schema (WIP) 저장 위치.
- `validation/` *(선언됨)*
  - 성능/품질 검증 로그(`perf_logs/`, `qa_reports/`)를 보관.
- `export/` *(선언됨)*
  - ReID/Edge Export 스크립트와 결과물 샘플.
- `logs/` *(선언됨)*
  - 세션 실행 로그를 날짜별로 정리.

## 사용 지침

1. 새로운 세션 템플릿을 만들면 `configs/`에 저장하고, 파일 상단에 주석(`_meta`)으로 목적/Phase를 적는다.
2. 검증 또는 벤치마크를 수행하면 결과 JSON/CSV를 `validation/` 하위에 넣고, 관련 설계 문서(예: 8_Performance_Benchmarks.md)에 링크를 남긴다.
3. 환경별 튜닝 스크립트(예: batch 실행, manifest 후처리)는 `export/` 또는 `logs/`에 두되, README를 업데이트해 사용법을 공유한다.

이 폴더는 코드와 분리된 운영 자산을 표준화하여 Session 재현성과 QA 추적성을 높이는 것이 목적이다.
