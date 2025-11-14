CCTV Synthetic Data Generation Engine (CCTV Synthetic Data Factory)

---

## 1. Overview

본 프로젝트는 실제 CCTV 환경에서 수집·라벨링하기 어려운 **대규모, 고품질, 정밀 라벨 합성 데이터**를
자동으로 생성하는 **CCTV 특화 Synthetic Data Generation Engine**(이하 “엔진”)을 구축하는 것을 목표로 한다.

생성 데이터의 주요 타겟 Task:

- Multi-Object Tracking (MOT)
- Person Re-Identification (ReID)
- Cross-camera ID consistency
- Occlusion/visibility reasoning
- Crowd behavior analysis
- 감염병 동선 추적, 안전/보안 감시, Edge NPU 모델 검증

엔진은 **“CCTV Synthetic Data Factory”** 로 동작하며,
사용자가 구성한 시나리오에 따라 다양한 환경·카메라·군중·랜덤화를 제어하며
반복 가능하고 재현 가능한 방식으로 dataset을 생산한다.

---

## 1.1 주요 타겟 사용자 (Persona)

### 1) 산업용 CCTV 분석 팀 리드

- 배경  
  - 공장, 물류창고, 병원, 군사/보안 시설 등에서 안전·보안 모델 PoC를 책임지는 담당자
- Pain Point  
  - 30일 내 PoC 요구 (기존: 데이터 수집 3개월 + 라벨링 2개월 이상)
  - 기밀·프라이버시 이슈로 촬영 자체가 어렵거나 허가 절차가 길다
  - 넘어짐, 화재, 침입 등 희귀 이벤트는 실제 촬영이 사실상 불가능
  - Edge NPU·저해상도 CCTV 환경에 맞는 데이터 부족
- 엔진 사용 시 기대 효과 (목표)
  - Time-to-PoC: 기존 대비 70~80% 단축
  - 라벨링 비용: 수작업 대비 80~90% 절감 (주로 하드웨어·운영 비용만 발생)
  - 실제 촬영이 어려운 환경/상황에 대한 합성 데이터 확보

### 2) MOT / ReID / 군중 분석 연구자

- 배경  
  - Cross-camera ReID, Occlusion, Crowd behavior 등
    기존 공개 dataset이 약한 영역을 연구하는 학계/산업 연구자
- Pain Point  
  - 실제 데이터는 카메라 위치·군중 밀도·occlusion 패턴을 세밀하게 통제하기 어렵다
  - 동일 조건을 재현하는 실험이 어렵고, 공개 dataset에 없는 조합이 많다
- 엔진 사용 시 기대 효과 (목표)
  - 실험 설계–데이터 생성–학습까지의 사이클 단축 (수주 → 수일 수준)
  - Config 기반 재현성 100%에 가까운 실험 환경
  - Cross-camera, occlusion, crowd 상황을 의도적으로 설계한 데이터 생성

---

## 1.2 대표 Use-case 시나리오

### UC-01: 공장 안전감시 모델용 데이터 생성

- 환경: Factory Scene + 안전 관련 행동 프리셋(넘어짐, 제한구역 진입 등)
- 카메라: 3~4대 고정형 CCTV, 서로 다른 FOV/높이/방향
- 조건: 주간/야간 혼합 조명, 중간 정도의 군중 밀도
- 결과:
  - 수십~수백만 프레임의 합성 데이터
  - Detection/Tracking/Global ID 라벨
  - Edge NPU 학습 포맷(TFLite/ONNX 등)으로 Export (Phase 3)

### UC-02: Cross-camera ReID 연구용 데이터 생성

- 환경: Office + Warehouse Scene을 조합한 Multi-Scene 구성
- 카메라:
  - 서로 다른 FOV, 높이, 노이즈 모델을 가진 6대 카메라
- 제어:
  - 의상/동선/군중 밀도/조명/Domain Randomization 강도 설정
- 결과:
  - Global Person ID + Appearance Feature(Embedding)를 포함한 dataset
  - 다양한 카메라 조합 및 occlusion 패턴을 커버하는 ReID 연구용 데이터

### UC-03: Sim-to-Real 연구 실험 설계

- 목표: Domain Randomization 강도에 따른 Sim-to-Real 성능 변화를 정량 분석
- 예시 실험:
  - Session A: Randomization 없음 (Baseline)
  - Session B: Low Randomization
  - Session C: Medium Randomization
  - Session D: High Randomization
- 각 세션별로 동일 환경/카메라 구성에서 서로 다른 랜덤화 강도로 데이터 생성
- Real-world CCTV dataset에서 평가하여
  - 어떤 수준의 랜덤화가 가장 좋은 성능으로 이어지는지 정량 비교
- 문서 상의 성능 수치는 “예상·참고 수준”으로만 사용하며,
  실제 프로젝트에서는 별도 실험을 통해 검증해야 한다.

---

## 2. Problem Statement

### 2.1 개인정보·법적 제약

- 얼굴, 걸음걸이, 체형, 동선은 민감 개인정보에 해당하며
  법적 수집·보관·활용에 강한 제한이 있다.
- 공장, 병원, 군사시설 내부는 촬영 허가 자체를 받기 어렵거나,
  촬영 후 외부 반출이 불가능한 경우가 많다.

### 2.2 라벨링의 한계

- Bounding box는 수작업 라벨링이 가능하나 비용이 매우 크다.
- Cross-camera ID, Occlusion ratio, Visibility ratio, Camera geometry 등은
  사람이 정확하게 라벨링하기 사실상 불가능하다.
- Tracking/ID consistency를 사람이 장시간 유지하는 것도 거의 불가능에 가깝다.

### 2.3 다양한 환경 구성의 어려움

- Factory, Office, Hospital, Warehouse, Military 등
  서로 다른 도메인의 실내·실외 환경을 실제로 촬영해 확보하는 것은
  시간·비용·허가 측면에서 비현실적이다.

### 2.4 Edge Device용 CCTV dataset 부족

- i.MX, EdgeTPU, Jetson 등 NPU 기반 Edge 디바이스를 고려한
  해상도, 포맷, 라벨 구조를 가진 CCTV dataset이 매우 부족하다.
- 공개 dataset은 연구 지향 포맷에 치우쳐 있고,
  실제 deployment 환경을 충분히 반영하지 못한다.

### 2.5 기존 솔루션의 한계

1) 공개 CCTV/MOT/ReID Dataset
- 환경이 제한적 (특정 거리/캠퍼스/도심 위주)
- Cross-camera ReID 및 Occlusion GT 부족
- Tracking/ID 라벨 일관성 문제
- 카메라 intrinsic/extrinsic 정보 거의 부재

2) 자율주행/게임용 시뮬레이터 (CARLA, AirSim, Unity ML-Agents 등)
- CCTV 특화 아님 (이동형 센서·차량 중심)
- Multi-camera 고정 CCTV 환경이 주된 목표가 아님
- Auto-labeling은 제공하더라도 ReID/Occlusion/Global ID 수준은 제공하지 않음

3) 상용 Synthetic Data 서비스
- 프레임당 과금 구조 → 수십~수백만 프레임 생성 시 비용 폭발
- 내부 시뮬레이션/라벨링 로직이 black-box여서 재현성·통제가 낮음
- CCTV 특화 시나리오(실내 공장/병원/군사 시설 등)에 대한 지원이 제한적

---

## 3. Goals & Scope

### 3.1 최종 목표

- 다양한 CCTV 환경(Scene)에서
  수십만~수백만 프레임 규모의 합성 데이터를 **안정적으로** 생성
- 완전 자동 라벨링:
  - Detection (bbox, confidence)
  - Tracking (frame-level track id)
  - Person Global ID (cross-camera identity)
  - Occlusion / Visibility ratio
  - Camera 메타데이터 (intrinsic/extrinsic, scene meta)
- ReID 학습용 Dataset Export:
  - Global ID 기반 person crop 자동 추출
  - FastReID/TorchReID 호환 디렉토리 구조
- Config 기반 재현성:
  - 동일 Config로 실행 시 동일 dataset 재생성
  - manifest.json에 세션/환경/라벨 통계 기록
- Edge-friendly 포맷 지원:
  - 학습용(YOLO/COCO/Custom JSON)
  - Edge 디바이스 대응 포맷(TFLite/ONNX/Custom binary 등)

### 3.2 Phase별 Scope 개요

- Phase 1 (MVP, 3개월): “단일 Scene, 단순 파이프라인, 기본 Detection/Tracking”
- Phase 2 (확장, 6개월): “Multi-Scene, Worker 파이프라인, Appearance/Randomization”
- Phase 3 (최적화, 9개월): “대규모 스케일, Validation/Stats 자동화, Edge Export”

아래에서 Phase별 상세 목표를 정의한다.

---

## 4. Core Concepts

### 4.1 Multi-Scene CCTV Simulation

- 지원 대상 Scene (중장기):
  - Factory, Office, Hospital, Warehouse, Military 등
- 구현 전략:
  - Unity 기반 Scene Pooling
  - Additive Load 후 Enable/Disable로 환경 전환
- Phase 1은 Factory 단일 Scene에 집중하고,
  이후 단계에서 Scene Pool 확장.

### 4.2 Multi-camera Configuration

- 고정형 CCTV + (선택적) Robot 이동형 카메라
- 서로 다른 FOV, 해상도, 노이즈, 색감, 위치/높이 설정
- Cross-camera ReID 연구에 충분한 카메라 구성 다양성 제공

### 4.3 Behavior-based Crowd Simulation

- NavMesh 기반 이동 및 충돌 회피
- Behavior Module (Walk, Idle, GroupMove 등)
- Crowd density, 행동 프리셋, 외형(Appearance) 랜덤화를 통해
  다양한 혼잡·행동 패턴을 생성
- Phase 1에서는 단순 Walk/Idle 위주로 시작하고,
  Phase 2 이후 점진적으로 복잡 행동 추가

### 4.4 Automatic Labeling Engine

매 프레임마다 엔진이 자동 생성하는 라벨:

- 2D Bounding box (3D GT → 2D projection)
- Track ID (camera-level temporal consistency)
- Person Global ID (Scene/Session 레벨 identity)
- Occlusion ratio (visible pixels / total bbox pixels)
- Visibility ratio (보이는 신체 비율)
- Camera intrinsic/extrinsic
- Scene/Scenario metadata

추가로 ReID 모델 학습을 위한 dataset export 기능:
- Global ID 기반 person crop 이미지 자동 추출
- FastReID/TorchReID에서 바로 학습 가능한 디렉토리 구조 제공

Simulation 내부에는 항상 "정답 GT"가 존재하므로,
수작업으로는 불가능한 정밀 라벨을 안정적으로 제공한다.

※ Synthetic GT는 시뮬레이션 관점에서 완벽하지만,
실제 Real 환경에는 Domain Gap이 존재하므로
Sim-to-Real 전략(랜덤화, 스타일 변환, Real fine-tuning 등)이 필요하다.

### 4.5 Parallel Data Pipeline (Worker-based)

- Capture → Label → Encode → Storage → Validation 순의 단계적 파이프라인
- 각 단계는 Worker + Queue 기반 비동기 처리
- PipelineCoordinator가 전체 흐름과 back-pressure를 관리
- Raw → Labeled → Encoded 3단 데이터 모델로 책임 분리

### 4.6 Dataset Quality Assurance Pipeline

- Technical Validation:
  - 파일 무결성, 이미지-라벨 매칭, 좌표/값 범위 검사
- Statistics:
  - frame count, 사람 수, detection 수, occlusion histogram, bbox 분포
- Manifest:
  - SessionConfig, Statistics, QualityMetrics를 manifest.json으로 생성
- Performance Validation (별도):
  - 실제 MOT/ReID 모델 학습 및 Real dataset 평가를 통한 Sim-to-Real 성능 확인

---

## 5. Phase별 개발 계획

### Phase 1: MVP – “기본 엔진 구축” (약 3개월)

**목표**

- 단일 Factory Scene 기반, 3대 고정 CCTV
- 기본 Crowd (20~30명, Walk/Idle 행동)
- Detection + Global ID + 간단 Tracking (frame 간 ID 유지)
- 동기식 캡처(ReadPixels 기반) + 단일 스레드/간단 파이프라인
- JPG + JSON 포맷 저장

**범위 (In Scope)**

- Factory Scene 1개 (Asset Store 위주 활용)
- SessionConfig를 통한 프레임 수, 해상도, 카메라 수 설정
- Bounding box + Person Global ID + 기본 Track ID
- 단순한 디렉토리 구조(image/label)
- 기본 Progress 표시 (현재 프레임, FPS 정도)

**제외 (Out of Scope)**

- Multi-Scene, Scene Pooling
- AsyncGPUReadback, Worker 기반 파이프라인
- ReID Dataset Export
- Occlusion/Visibility 계산
- Domain Randomization
- YOLO/COCO Export
- Validation/Stats 자동화

### Phase 2: 확장 – “Production Ready에 근접” (약 6개월)

**목표**

- 3~5개 Scene + Scene Pooling
- 4~6대 이상의 카메라 구성
- AsyncGPUReadback 기반 비동기 캡처
- Worker 파이프라인 (Capture → Label → Encode → Storage)
- ReID 학습용 Dataset Export (person crop 자동 추출)
- 기본적인 Domain Randomization (조명, 노이즈, 색감)
- YOLO/COCO 포맷 Export
- Checkpoint 기반 세션 재시작

**범위**

- Multi-Scene 환경: Factory, Office, Warehouse 등 확장
- LabelWorker 기능 확장:
  - Detection, Tracking, Global ID
  - ReID Dataset Export Worker 추가
- NFR 관점 초기 달성:
  - 중간 규모(예: 50만 프레임) 데이터 안정 생성
  - 6카메라 기준 목표 FPS 설정(현실 가능한 수치로 재평가 후 결정)
- 기본 Validation/Stats:
  - 누락 프레임, 기본 통계, manifest 생성

**선택/실험적 기능**

- Occlusion/Visibility 계산 (Beta)
- 이동형 Robot 카메라 지원 (기본 구현)

### Phase 3: 최적화 – “Enterprise Scale” (약 9개월)

**목표**

- 100만 프레임 이상 장시간 안정 생성
- 고정밀 Occlusion/Visibility 계산
- ValidationService + StatsService 자동화
- Multi-GPU 혹은 Multi-session 기반 처리(설계·기술적 타당성 검토 후 범위 조정)
- Edge NPU 포맷 Export(TFLite/ONNX/Custom binary)
- CLI + Web UI

**범위**

- 성능·안정성 튜닝:
  - GPU/CPU 사용률 관리
  - 파이프라인 back-pressure 최적화
  - I/O 병목 감소
- Dataset QA 체계 완성:
  - 자동 Validation, Summary, manifest 기반 리포트
- Edge Export:
  - 경량/압축 포맷, low-resolution 설정 옵션 등

**선택 기능**

- Style Transfer(CycleGAN 등) 통한 Real-like 스타일 변환
- Multi-machine 분산 생성
- 복잡한 행동 프리셋(넘어짐, 싸움, 그룹 상호작용 등)

---

## 6. 리스크 및 제약 조건

### 6.1 기술적 리스크

- 현실감 있는 Scene 제작 난이도 (특히 Factory/Office/Hospital)
- Unity 메인 스레드 제약으로 인한 Worker/병렬 구조 구현 난도
- AsyncGPUReadback 성능 및 안정성
- 대규모 I/O(수백 GB~TB)의 디스크 병목
- Synthetic → Real Domain Gap으로 인한 실제 성능 저하 가능성

### 6.2 팀 역량 및 리소스

- Unity/Rendering 전문가 필요
- 3D 아티스트 또는 외주 협업 필요
- 병렬 처리·파일 I/O·백엔드 경험 있는 개발자 필요
- ML 엔지니어를 통한 MOT/ReID 성능 검증 필요

### 6.3 하드웨어/운영 제약

- 최소: RTX 30xx급 GPU, 32GB RAM, SSD 500GB 이상
- 권장: 상위급 GPU, 대용량 NVMe SSD, 충분한 RAM
- 장시간 생성 시 전력·발열·장비 안정성 고려 필요

---

## 7. 윤리적 사용 가이드라인

### 7.1 허용 용도 (예시)

- 안전/보안 시스템 개발·연구
- 감염병 동선 추적 시스템 사전 훈련
- 학술 연구 (MOT, ReID, Crowd Analysis 등)
- Edge AI 성능 검증 및 최적화

### 7.2 금지 용도 (예시)

- 실제 개인 감시를 목적으로 한 사용
- 특정 실존 인물의 외형을 의도적으로 모사하는 데이터 생성
- 차별적·악의적 목적의 데이터 생성·활용
- 법적 규제 회피를 위한 우회 도구로의 사용

### 7.3 데이터 생성 원칙

- 모든 인물·환경은 합성이며, 실존 인물을 직접 모사하지 않는다.
- 인종·성별·연령 등에서 특정 집단의 과소·과대 표현을 피하고 다양성을 확보한다.
- 생성된 데이터는 “합성 데이터”임을 명시해야 한다.
- 각 사용자는 소속 기관의 윤리·법적 절차를 준수해야 하며,
  엔진은 이를 보조하는 도구 역할만 수행한다.

---

## 8. 기대 효과 (정량·정성)

### 8.1 정량적 목표 (지향점)

- Time-to-Dataset: 기존 촬영+라벨링 대비 70~90% 단축
- 라벨링 비용: 수작업 대비 80~90% 절감
- 라벨 품질: 시뮬레이션 기반 GT로 bbox/ID 일관성 크게 향상
- Sim-to-Real:
  - 적절한 Domain Randomization + Real fine-tuning 조합 시
    MOT/ReID 성능 향상 가능성 (정확한 수치는 실제 실험으로 검증)

### 8.2 정성적 가치

- 연구:
  - 재현 가능한 실험 환경
  - 기존 dataset에서 불가능했던 설정(카메라/군중/occlusion)을 실험 변수로 사용 가능
- 산업:
  - PoC 속도 향상
  - 민감 시설에서도 프라이버시 침해 없이 AI 개발 가능
- 플랫폼:
  - 향후 Dataset-as-a-Service, On-premise/Cloud 하이브리드 제품으로 확장 가능