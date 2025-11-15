## C# Backend 초기 구성 가이드 (Planned)

이 디렉터리는 Phase 1부터 C# Orchestration, Simulation Adapter, Data Pipeline 코드를 포함할 예정이며, 현재는 스캐폴드만 정의되어 있다.

### 목표 네임스페이스

- `Forge.Application`: CLI/Config/Progress 처리
- `Forge.Orchestration`: Session/Scenario/Pipeline 조정
- `Forge.Simulation`: Unity Adapter 및 IPC
- `Forge.DataPipeline`: Worker/Queue 구현
- `Forge.Services`: Validation/Stats/Manifest

### 초기화 체크리스트

자동 스크립트: `src/init_sln.sh` (dotnet SDK 필요)

1. `./init_sln.sh` 실행 → `Forge.sln`과 Class Library 프로젝트 자동 생성
2. `Forge.Application` 프로젝트에 CLI 엔트리포인트(`Program.cs`) 생성
3. Projects 간 참조는 스크립트가 기본 세팅하며, 필요 시 `Directory.Build.props`에서 TargetFramework 변경
4. `docs/design/3_Class_Design_Document.md`의 클래스 책임을 그대로 반영하며, `/status` API에서 보고되는 `engineVersion/supportedVersions/authMode` 필드를 `ProgressReporter`에 연결

> 상태: planned – 실제 구현은 Phase 1 kick-off 시 착수
