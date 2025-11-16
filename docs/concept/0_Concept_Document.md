## 1. Overview

Forge는 다양한 현장 감시·로봇·시설 카메라 환경에서 수집·라벨링하기 어려운 **대규모·고품질·정밀 라벨 합성 데이터셋**을 **반복 가능(reproducible)** 하고 **온프레미스에서도 안전하게** 자동 생성하는 엔진이며, 특히 **고정형 + 모바일(로봇) 카메라**를 동시에 제어·생성할 수 있는 것이 차별 요소다.

Forge는 다음과 같은 Vision 기반 Task를 타겟으로 한다:
- Multi-Object Tracking (MOT)
- Person Re-Identification (ReID)
- Cross-camera ID consistency
- Occlusion / Visibility reasoning
- Crowd behavior analysis
- 감염병 동선 추적 시뮬레이션
- 현장 카메라 기반 Edge NPU 모델 검증
- Sim-to-Real 연구

특히 **Occlusion / Visibility reasoning**은 ReID·Tracking 품질을 좌우하는 핵심 GT이므로, Scene·카메라 구성을 설계하는 초반 단계에서부터 “가려짐/시야 확보”를 핵심 문제로 다룬다.

또한 합성 데이터 신뢰성을 보증하기 위해 **Validation/Statistics/Manifest** 기반 Dataset 품질 검증이 상단 개념 설계에 포함되어 있다.

Forge는 사용자 정의 **Scenario/Scene/Camera/Crowd/Randomization**을 바탕으로  **정밀한 GT(ground truth)** 를 갖춘 대량 데이터셋을 생성하며,  동일 Config로 반복 실행 시 **동일한 데이터셋 재생성**이 보장된다.

---

## 1.1 주요 타겟 사용자 (Persona)

### 1) 산업용 영상 분석 팀 리드
- **배경**
    - 공장·물류·병원·군사·보안 시설 등에서 안전/보안 AI 모델 PoC 담당
    - 실차 환경 촬영과 라벨링에 매우 높은 비용·시간·허가 절차 필요
- **Pain Point**
    - PoC 타임라인 압박 (30일 내 결과 요구)
    - 촬영 자체가 어려운 프라이버시/보안 환경
    - 넘어짐·침입·화재 등 희귀 이벤트 촬영 불가
    - Edge 장비(NPU/현장·모바일 카메라) 특화 데이터 부족
- **Forge 기대 효과**
    - Time-to-PoC: 70~80% 단축
    - 라벨링 비용 절감: 80~90%
    - 기존 촬영 불가능한 환경·상황·조명을 시뮬레이션으로 확보
    - 다양한 Edge 환경 포맷(TFLite/ONNX/저해상도) 생성 가능

### 2) MOT / ReID / 군중 분석 연구
- **배경**
    - ReID, Cross-camera tracking, Occlusion, Crowd density 연구 다수
    - 기존 공개 dataset의 한계로 실험 변수 제어가 어렵고 재현 실험 불가능
- **Pain Point**
    - 동일 실험 조건을 여러 번 반복하기 어려움
    - 공개 dataset은 도메인 편향(캠퍼스/거리/도심 등)
    - Cross-camera 또는 occlusion GT가 부족함
- **Forge 기대 효과**
    - Config 기반 100% 재현 실험
    - Scene/Camera/Crowd/Occlusion/노이즈/조명 완전 제어
    - 실험–데이터 생성–학습 사이클이 **수주 → 수일** 수준으로 단축

---

## 1.2 대표 Use-case 시나리오

### UC-01: 공장 안전/로봇 감시 모델용 Dataset 생성
- Scene: Factory + 안전 관련 행동 프리셋(넘어짐·제한구역 진입 등)
- Camera: 3~4대 카메라(고정형 + 이동형 로봇) (FOV/높이 다양)
- Condition: 주간/야간 조명 랜덤화, 중간 수준 crowd density
- Output:
    - 수십~수백만 프레임
    - Detection/Tracking/Global ID
    - Edge-friendly Export (TFLite/ONNX 등) — Phase 3

### UC-02: Cross-camera ReID 연구용 데이터 생성

- Scene: Office + Warehouse → Multi-Scene
- Camera: 6대(고정형/이동형 혼합), 각기 다른 FOV/노이즈/높이
- Control:
    - 의상/동선/조명/군중 밀도 랜덤화
    - 다양한 occlusion 상황 생성
- Output:
    - Global ID 기반 ReID용 crop dataset
    - 다양한 카메라 조합과 appearance 분포 확보

### UC-03: Sim-to-Real 연구 실험 설계

- 목표: Randomization 강도 변화(None/Low/Med/High)에 따른 Sim→Real 성능 곡선 분석
- 시나리오:
    - 동일 Scene + 동일 Camera 구성
    - Randomization만 달리하여 여러 Session 지속 생성
    - Real 현장 카메라 dataset으로 평가
- Forge 역할:
    - 재현 가능한 실험 환경 제공
    - 랜덤화 강도 조절
    - 통계/manifest 기반 자동 품질 요약 제공

### UC-04: 로봇 카메라 기반 감시/순찰 시나리오

- Scene: 병원·물류창고·주차장 등 이동 경로가 긴 시설 공간
- Camera: 고정형 감시 카메라 + SLAM 기반 모바일 로봇 카메라(순찰 경로 다중 프리셋)
- Control:
    - 로봇 속도/경로/회전 각도 랜덤화
    - 움직이는 군중이 로봇 시야를 가리는 occlusion 이벤트 집중 생성
    - Edge NPU용 저해상도/와이드 FOV 설정
- Output:
    - 로봇 시야 중심의 Tracking/ReID 데이터
    - 모바일 카메라 전용 occlusion/visibility GT
    - 순찰 루틴별 QA/Validation 통계 로깅

---

# 2. Problem Statement

## 2.1 개인정보·법적 제약

- 감시 영상은 얼굴·동선·걸음걸이 등 민감 정보 포함
- 공장/병원/군사 시설 촬영 자체가 어려움
- 데이터 반출 불가/보안 규정 등으로 학습 데이터 확보가 매우 제한적

## 2.2 라벨링의 불가능성

- bbox 정도는 가능해도 cross-camera ID, occlusion ratio, visibility, geometry 기반 라벨은 **사람이 직접 라벨링할 수 없음**
- Tracking consistency도 장시간 유지 불가

## 2.3 다양한 환경 구성의 어려움

- 다양한 도메인의 실내·실외 환경 촬영은 비용·시간·허가 모두 비현실적
- 조명/군중/동선/노이즈 변수 제어가 실제 환경에서는 거의 불가능

## 2.4 Edge Device용 감시/현장 영상 Dataset 부족

- 공개 dataset 대부분 고해상도·연구용 포맷 중심
- 산업용 저해상도/노이즈 많은 Edge 환경을 제대로 반영하지 못함
- NPU 최적화 포맷(TFLite/ONNX/Custom binary)도 극도로 부족

## 2.5 기존 솔루션의 한계

1. 공개 ReID/감시 Dataset
	- 환경 제한적
	- cross-camera GT 부족
	- camera metadata 거의 없음
	- occlusion/visibility GT 없음
2. 자율주행/게임 시뮬레이터
	- 감시/현장 목적 아님
	- Global ID/Track ID/occlusion 등 정밀 라벨링 제공 안 함
3. 상용 Synthetic 서비스
	- 프레임당 과금 구조 → 대규모 생성 시 비용 폭발
	- 내부 로직 black-box → 재현성 부족
	- Factory/Hospital 등 domain-specific 기능 부족

---

## 3. Goals & Scope

### 3.1 최종 목표

Forge의 최종 목표는 아래와 같다:

- 다양한 현장 Scene에서 수십만~수백만 프레임 안정 생성
- 완전 자동 라벨링 제공:
	- bbox  
	- tracking 
	- global ID
	- occlusion/visibility
	- camera meta (intrinsic/extrinsic)
- ReID dataset 자동 Export
- Config 기반 100% 재현성
- Edge-friendly 포맷 지원(TFLite/ONNX 등)
- Validation/Statistics/Manifest 기반 Dataset QA 내장

---

### 3.2 Phase별 Scope (개요)

#### **Phase 1: MVP (3개월)**

- Factory 단일 Scene  
- 3대 카메라(고정형 위주, 이동형 1대 시범 도입)  
- 20~30명 crowd / Walk/Idle  
- 동기식 캡처  
- 기본 Tracking + Global ID  
- JSON + JPG Export  
- 기본 progress 표시  

#### **Phase 2: 확장 (6개월)**

- Multi-Scene(3~5개)  
- 6대 카메라(고정형 + 모바일 로봇 혼합)  
- AsyncGPUReadback  
- Worker 파이프라인  
- ReID Export  
- Domain Randomization  
- Checkpoint/Resume  
- YOLO/COCO Export  
- 기본 Validation/Stats  

#### **Phase 3: 최적화 (9개월)**

- ≥ 1M 프레임 장시간 안정 생성  
- Occlusion/visibility 고정밀 계산  
- Validation/Stats 자동화  
- Multi-GPU 검토  
- Edge Export(TFLite/ONNX/binary)  
- Web UI + CLI  
- Style Transfer(선택)  

---

## 4. Core Concepts

### 4.1 Multi-Scene Field Simulation

- Factory/Office/Warehouse/Hospital/Military  
- Unity 기반 Scene Pooling + additive load 전환  
- Scene metadata 제공 (intrinsic/extrinsic, lighting, obstacles)

### 4.2 Multi-camera (Static + Mobile) Configuration

- FOV/높이/방향/노이즈/색감/해상도 조절  
- Cross-camera ReID 연구까지 고려한 시야/각도 배치 다양성 제공  

### 4.3 Behavior-based Crowd Simulation

- NavMesh + Behavior Module  
- Walk/Idle/GroupMove (Phase2 이후 확장)  
- 외형/의상/속도/이동 패턴 다양화  

### 4.4 Automatic Labeling Engine

- bbox / track_id / global_id  
- occlusion / visibility  
- camera meta  
- scenario meta  
- ReID crop export (global_id 기반)

### 4.5 Parallel Data Pipeline

- Capture → Label → Encode → Storage → Validation  
- Worker 기반 병렬 처리  
- back-pressure 제어  
- Raw/Labeled/Encoded 데이터 모델 분리  

### 4.6 Dataset Quality Assurance

- Validation: 파일·좌표·라벨 무결성  
- Statistics: frame/detection/person/occlusion histogram  
- Manifest: config + stats + quality metrics  
- Performance QA(외부 ML 평가)는 별도  

---

## 5. 리스크 및 제약 조건

### 5.1 기술 리스크

- Scene 제작 난이도  
- Unity 메인 스레드 제약  
- AsyncGPUReadback 안정성  
- 대규모 I/O 병목  
- Domain Gap 문제  

### 5.2 팀 리소스 리스크

- Unity/Rendering 전문가 필요  
- 3D 모델링 소스 필요  
- 병렬·파일 I/O 백엔드 엔지니어 필요  
- ML 엔지니어 통한 성능 검증 필요  

### 5.3 하드웨어 제약

- 최소 RTX 30xx / 32GB RAM / 500GB SSD  
- 장시간(수일) 생성 시 발열·전력·안정성 고려  

---

## 6. 윤리적 가이드라인

### 6.1 허용 용도

- 안전/보안/감염병 대응 연구  
- MOT/ReID/Crowd 연구  
- Edge NPU 검증  
- 학술 연구  

### 6.2 금지 용도

- 실존 인물 감시 목적  
- 특정 인물 외형 의도적 모사  
- 차별적/악의적 용도  
- 법적 규제 회피 목적  

### 6.3 생성 데이터 원칙

- 모든 인물·환경은 합성  
- 특정 집단 과소/과대 표현 금지  
- “합성 데이터”임을 명시  
- 각 사용자는 법·윤리 규정 준수  

---

## 7. 기대 효과

### 7.1 정량 목표

- Time-to-Dataset 70~90% 단축  
- 라벨링 비용 80~90% 절감  
- bbox/ID 일관성 실측 기반 정밀 라벨 제공  
- Sim-to-Real 성능 개선 가능성  

### 7.2 정성 가치

- 재현 가능한 실험 환경  
- 실제 촬영 불가능한 환경/상황 생성  
- 기관·기업 내 온프레미스 전용 데이터 생성 가능  
- 향후 Dataset-as-a-Service / Cloud-on-Prem 모델로 확장 가능  
