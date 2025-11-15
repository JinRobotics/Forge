# Forge

Synthetic CCTV Data Generation Engine - Unity 기반 합성 데이터 생성 시스템

---

## 🎯 프로젝트 개요

실제 CCTV 환경에서 발생하는 다양한 시나리오 (가림, 조명 변화, 혼잡도 변화 등)를 시뮬레이션하여 대규모 라벨링된 합성 데이터셋을 생성합니다.

**주요 응용 분야**:
- Multi-Object Tracking (MOT)
- Person Re-Identification (ReID)
- Cross-camera ID consistency
- Occlusion/visibility reasoning
- Edge NPU 최적화 모델 학습

---

## 📂 프로젝트 구조

### 현재 디렉터리

```
forge/
├── docs/                 # 컨셉·요구사항·설계 문서
│   ├── concept/
│   │   ├── 0_Concept_Document.md
│   │   └── 1_User_Requirements.md
│   └── design/
│       ├── 0_Repo Structure.md
│       ├── 2_System_Requirements.md
│       ├── 3_System_Architecture.md
│       ├── 4_Class_Design_Document.md
│       ├── 5_Data_Pipeline_Specification.md
│       └── 5. API Specification.md
├── reference_repo/       # Unity Perception 참고용 원본 저장소
├── src/                  # (비어 있음) C# 엔진 코드 예정
└── README.md
```

- Unity Perception 분석 초안은 `reference_repo/docs_analysis/` 하위에 위치하며, 필요 시 `docs/analysis/`로 승격 예정이다.
- `src/`는 현재 빈 폴더로, Phase 1 개발 시작 시 Orchestration/Simulation/Pipeline 코드가 추가될 예정이며 주요 네임스페이스는 `Forge.Application`, `Forge.Orchestration` 등으로 계획되어 있다.
- `reference_repo/`에는 아래와 같은 외부 예제/패키지를 그대로 보관한다.
  - `SynthDet/`
  - `Unity-Robotics-Hub/`
  - `com.unity.perception/`
  - `docs_analysis/` (Unity Perception 분석 초안)
  필요 파일만 단계적으로 추출해 사용할 예정이므로 빌드 소스와 혼동하지 말 것.

### 목표 구조(설계 기준)

설계 문서에서 제시한 전체 구조가 궁금하면 `docs/design/0. Repo Structure.md`를 참고하라. 해당 문서는 **계획된 구조**를 설명하며, 실제 디렉터리와 이름/위치가 다를 수 있다.

---

## 🚀 빠른 시작

### 1. 시뮬레이션 실행

1. **Unity 프로젝트 열기**  
   `unity/` 폴더를 Unity 2021 LTS로 오픈
2. **시뮬레이션 빌드**  
   Unity에서 Build → 합성 전용 실행 파일 생성
3. **세션 실행 (예시)**  
   ```bash
   dotnet run --config pipeline/configs/session_example_factory.json
   ```
   원격/보안 환경이면 `--api-key <KEY>` 또는 환경 변수 `FORGE_API_KEY` / `FORGE_BEARER`를 설정해 CLI가 자동으로 `X-Api-Key`/`Authorization` 헤더를 붙이도록 한다.
   - Unity를 별도 프로세스로 띄우는 경우 config에 `simulationGateway`를 포함한다:
   ```json
   {
     "sessionId": "session_factory_run_001",
     "totalFrames": 100000,
     "outputDirectory": "/data/output/session_factory_run_001",
     "simulationGateway": {
       "mode": "remote",
       "host": "127.0.0.1",
       "port": 8080,
       "auth": { "type": "api-key", "apiKeyEnv": "FORGE_API_KEY" },
       "allowedHosts": ["127.0.0.1"]
     },
     "...": "..."
   }
   ```
4. **API 상태 점검 (선택)**  
   ```bash
   pipeline/scripts/status_smoke.sh
   ```  
   `SIM_ENDPOINT`, `FORGE_API_KEY`, `FORGE_BEARER` 환경 변수를 설정하면 `/status` 응답의 `engineVersion`, `supportedVersions`, `authMode`를 확인할 수 있다.

> GitHub Actions에서 주기적으로 상태를 점검하려면 `.github/workflows/status_smoke.yml`을 사용하고 동일한 값을 Secrets(`SIM_ENDPOINT`, `FORGE_API_KEY`, `FORGE_BEARER`)에 저장한다.

### 2. 문서 읽기

**프로젝트 컨셉 파악** (30분):
```
docs/concept/0_Concept_Document.md
```

**Phase별 계획 이해** (1시간):
```
docs/design/2_System_Requirements.md
docs/design/3_System_Architecture.md
```

### 3. Unity Perception 분석 (아키텍트/개발자)

**Unity Perception 이해하기**:
```
reference_repo/docs_analysis/00_START_HERE.md
```

---

## 📚 핵심 문서

| 문서 | 설명 | 대상 |
|------|------|------|
| [Concept Document](docs/concept/0_Concept_Document.md) | 프로젝트 비전, 페르소나, 리스크 분석 | 전체 |
| [User Requirements](docs/concept/1_User_Requirements.md) | 사용자 요구사항 (UR-01~12) | PM, 기획자 |
| [System Architecture](docs/design/3_System_Architecture.md) | 4-Layer 아키텍처 설계 | 아키텍트, 개발자 |
| [Class Design](docs/design/4_Class_Design_Document.md) | 클래스 구조 및 인터페이스 | 개발자 |
| [Perception Analysis](reference_repo/docs_analysis/00_START_HERE.md) | Unity Perception 이해 분석 | Unity 개발자 |

---

## 🗓️ 개발 Phase

### Phase 1 (MVP - 3개월)
- 단일 환경 (Factory)
- 3카메라 × 10 FPS
- Detection + Tracking 라벨
- 10만 프레임 생성

### Phase 2 (확장 - 6개월)
- 5개 환경 + Scene Pooling
- 6카메라 × 15 FPS
- Appearance Feature, Domain Randomization
- 50만 프레임 생성

### Phase 3 (최적화 - 9개월)
- Occlusion/Visibility
- Multi-GPU 병렬화
- 100만 프레임 목표
- Edge NPU 최적화

---

## 🛠️ 기술 스택

- **Engine**: Unity 2021 LTS+
- **Language**: C#
- **Base Framework**: Unity Perception (customized)
- **GPU**: NVIDIA RTX 3070+ (AsyncGPUReadback)
- **Output**: JSON, YOLO, COCO, TFLite, ONNX

### 결과물 디렉터리/manifest 개요
- 기본 출력: `images/`, `labels/json|yolo|coco/`, `meta/manifest.json`
- Edge Export(Phase 3+): `edge_packages/` 아래 `tflite/`, `onnx/`, `custom_binary/` 등 포맷별 디렉터리 생성
- manifest 추가 필드 예시:
```json
"edgeArtifacts": [
  {"format": "tflite-record", "path": "edge_packages/tflite/data.record", "checksum": "sha256:...", "status": "ready"}
]
```

---

## 📖 기여 가이드

TBD

---

## 📧 연락처

- 프로젝트 담당: TBD
- 기술 문의: TBD

---

**분석 생성일**: 2025-11-14
**현재 상태**: 🟢 설계 완료, 개발 착수 준비 완료
