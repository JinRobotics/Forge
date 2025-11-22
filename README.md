# Forge: Synthetic Data Generation Engine

Forge는 **Unity Perception** 기반의 합성 데이터 생성 엔진입니다.
로봇 공학 및 컴퓨터 비전 모델 학습을 위한 고품질의 라벨링된 데이터셋(SOLO, COCO 등)을 자동으로 생성합니다.

## 🚀 현재 상태 (Current Status)
**Phase 1 완료 (2025.11)**
- **Core Engine**: Unity Perception 패키지 통합 완료.
- **Authoring**: `SceneEditorWindow`를 통한 카메라 및 Randomizer 설정.
- **Web Dashboard**: 실시간 시뮬레이션 상태 모니터링 (Node.js + Unity HTTP).
- **Data Pipeline**: SOLO 포맷의 데이터셋 자동 생성.

---

## 🛠️ 시작하기 (Getting Started)

### 필수 요구사항 (Prerequisites)
- **Unity 2022.3 LTS** 이상
- **Node.js 16+** (웹 대시보드용)

### 1. Unity 프로젝트 설정
1. Unity Hub에서 `Forge/UnityProject` 폴더를 엽니다.
2. 프로젝트가 열리면 자동으로 필요한 패키지(`com.unity.perception` 등)를 로드합니다.
3. `Assets/Samples` 폴더에 테스트용 에셋(SynthDet)이 포함되어 있습니다.

### 2. 웹 대시보드 설정
터미널에서 서버 디렉토리로 이동하여 의존성을 설치하고 실행합니다.

```bash
cd Forge.Server
npm install
node server.js
```
서버가 실행되면 브라우저에서 `http://localhost:3000/dashboard`로 접속합니다.

---

## 📖 사용 방법 (Usage)

### 1. 씬 저작 (Authoring)
1. Unity 상단 메뉴에서 **Forge > Scene Editor**를 클릭합니다.
2. **Session ID**를 입력합니다 (예: `test_session_01`).
3. 씬 뷰(Scene View)에서 원하는 구도를 잡고 **Add Camera at View**를 클릭합니다.
   - 자동으로 `PerceptionCamera`와 `RandomizerTag`가 부착된 카메라가 생성됩니다.
4. `Assets/Samples`의 프리팹을 씬에 배치하여 환경을 꾸밉니다.

### 2. 시뮬레이션 실행 (Simulation)

#### 2.1 ForgeEngine 설정
1. Unity **Hierarchy** 창에서 우클릭 → **Create Empty**를 선택합니다.
2. 생성된 GameObject의 이름을 `ForgeEngine`으로 변경합니다.
3. `ForgeEngine`을 선택한 상태에서 **Inspector** 창 하단의 **Add Component** 버튼을 클릭합니다.
4. 다음 컴포넌트들을 순서대로 추가합니다:
   - `SessionManager` (검색창에 "SessionManager" 입력)
   - `ForgeScenario` (검색창에 "ForgeScenario" 입력)
   - `SimulationServer` (검색창에 "SimulationServer" 입력)

#### 2.2 ForgeScenario 설정
1. **Inspector** 창에서 `ForgeScenario` 컴포넌트를 찾습니다.
2. **Constants** 섹션을 펼칩니다.
3. **Iteration Count** 필드에 생성할 프레임 수를 입력합니다 (예: `100`).
4. (선택사항) **Random Seed** 값을 설정하여 재현 가능한 시뮬레이션을 만들 수 있습니다.

#### 2.3 SimulationServer 설정
1. **Inspector** 창에서 `SimulationServer` 컴포넌트를 찾습니다.
2. **Port** 필드가 `8080`으로 설정되어 있는지 확인합니다 (기본값).

#### 2.4 시뮬레이션 실행
1. Unity 에디터 상단의 **Play** 버튼 (▶️)을 클릭합니다.
2. **Console** 창을 열어 다음 로그를 확인합니다:
   ```
   [SimulationServer] Listening on port 8080
   [ForgeScenario] Configured: Iterations=100, Seed=...
   ```
3. 시뮬레이션이 진행되면서 프레임이 생성됩니다.
4. 웹 대시보드에서 실시간 진행률을 확인할 수 있습니다.

### 3. 모니터링 (Monitoring)
1. 시뮬레이션이 실행되는 동안 웹 대시보드(`http://localhost:3000/dashboard`)를 확인합니다.
2. 실시간 **FPS**, **진행률(Progress)**, **현재 프레임** 정보를 볼 수 있습니다.

### 4. 결과 확인 (Output)
시뮬레이션이 종료되면 프로젝트 폴더 내(또는 설정된 경로)에 데이터셋이 생성됩니다.
- 경로 예시: `Library/Perception/...` 또는 `User/Data/...`
- 파일: `captures_000.json`, `metrics_000.json` (SOLO 포맷)

---

## 📂 프로젝트 구조 (Structure)
```
Forge/
├── UnityProject/          # Unity 엔진 프로젝트
│   ├── Assets/
│   │   ├── Scripts/Core/  # 핵심 로직 (Session, Scenario)
│   │   ├── Scripts/Editor/# 에디터 툴 (SceneEditorWindow)
│   │   └── Samples/       # 테스트용 에셋 (SynthDet)
│   └── Packages/          # Unity Perception 패키지
├── Forge.Server/          # 웹 대시보드 백엔드 (Node.js)
└── docs/                  # 설계 및 가이드 문서
```
