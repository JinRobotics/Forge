# Forge Unity 프로젝트

이 디렉토리는 Forge 합성 데이터 생성 엔진의 Unity 프로젝트입니다.

## 프로젝트 구조

- `Assets/Scripts/Core`: 코어 로직 (Session, Scenario, Randomizer)
- `Assets/Scripts/Editor`: 에디터 도구 (Scene Editor Window)
- `Assets/Scripts/Runtime`: 런타임 전용 로직 (향후 Headless 빌드용)
- `Assets/Samples`: 테스트용 에셋 (SynthDet에서 가져온 3D 객체 및 텍스처)
- `Packages/com.unity.perception`: Unity Perception 패키지 (로컬)

## 시작하기

1. **Unity Hub에서 프로젝트 열기**
   - Unity 2022.3 LTS 이상 권장
   - 프로젝트를 열면 Perception 패키지가 자동으로 로드됩니다

2. **씬 에디터 사용**
   - 상단 메뉴: `Forge > Scene Editor`
   - 카메라 배치 및 SessionConfig 내보내기

3. **시뮬레이션 실행**
   - `ForgeEngine` GameObject 생성
   - `SessionManager` + `ForgeScenario` 컴포넌트 추가
   - Play 버튼 클릭

## 주요 컴포넌트

### ForgeScenario
Unity Perception의 `FixedLengthScenario`를 래핑하여 Forge 시스템과 통합합니다.

### SessionManager
외부 설정(JSON)을 로드하고 시뮬레이션 라이프사이클을 관리합니다.

### SceneEditorWindow
에디터에서 카메라 배치 및 설정을 시각적으로 편집할 수 있는 도구입니다.

## 문제 해결

- **Perception 패키지 인식 안 됨**: `Packages/manifest.json` 확인
- **컴파일 에러**: Unity 버전이 2022.3 LTS 이상인지 확인
- **웹 대시보드 연동 안 됨**: `SimulationServer` 컴포넌트가 ForgeEngine에 부착되어 있는지 확인
