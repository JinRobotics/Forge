# Concept & User Requirements

본 문서는 Forge의 개념적 정의와 사용자 요구사항을 통합한 기준 문서다. 상위 개념(비전·페르소나·문제 정의)과 정량/정성 요구사항을 한 곳에 모아 설계/구현/테스트의 추적성을 유지한다.

---

## 1. Concept Overview

Forge는 다양한 현장 감시·로봇·시설 카메라 환경에서 수집·라벨링하기 어려운 **대규모·고품질·정밀 라벨 합성 데이터셋**을 **반복 가능(reproducible)** 하고 **온프레미스에서도 안전하게** 자동 생성하는 엔진이며, 특히 **고정형 + 모바일(로봇) 카메라**를 동시에 제어·생성할 수 있는 것이 차별 요소다.

타겟 Vision Task:
- Multi-Object Tracking (MOT)
- Person Re-Identification (ReID)
- Cross-camera ID consistency
- Occlusion / Visibility reasoning
- Crowd behavior analysis
- 감염병 동선 추적 시뮬레이션
- 현장 카메라 기반 Edge NPU 모델 검증
- Sim-to-Real 연구

Forge의 핵심 가치:
- Occlusion/Visibility GT를 설계 단계부터 주력으로 다룸
- Validation/Statistics/Manifest 기반 품질 검증 포함
- Scenario/Scene/Camera/Crowd/Randomization을 조합해 정밀 GT 생성
- 동일 Config 재실행 시 동일 데이터셋 재생성 보장

---

## 2. 주요 타겟 사용자 (Persona)

### 1) 산업용 영상 분석 팀 리드
- 배경: 공장·물류·병원·군사·보안 시설 PoC 담당, 촬영/라벨링 비용·시간·허가 부담
- Pain Point: 촬영 불가 환경, 희귀 이벤트, Edge 장비 포맷 부족
- 기대 효과: Time-to-PoC 70~80% 단축, 라벨링 비용 80~90% 절감, 시뮬레이션으로 촬영 불가 환경 커버

### 2) MOT / ReID / 군중 분석 연구
- 배경: Cross-camera/occlusion 연구, 공개 dataset 제약
- Pain Point: 실험 재현성 낮음, 도메인 편향, occlusion GT 부족
- 기대 효과: Config 기반 100% 재현, Scene/Camera/Crowd/노이즈/조명 완전 제어, 실험 사이클 단축

---

## 3. 대표 Use-case 시나리오

- UC-01: 공장 안전/로봇 감시용 대량 Dataset (고정+이동 카메라, 조명 랜덤화, 수십~수백만 프레임)
- UC-02: Cross-camera ReID (멀티 Scene/카메라, occlusion 다양화, Global ID crop)
- UC-03: Sim-to-Real 연구 (Randomization 강도 비교, 재현 실험)
- UC-04: 로봇 순찰 감시 (SLAM 기반 모바일 카메라, occlusion 이벤트 집중, Edge NPU 포맷)

---

## 4. Problem Statement
- 개인정보·법적 제약: 보안/프라이버시 환경 촬영 불가, 데이터 반출 제한
- 라벨링 불가능성: cross-camera ID, occlusion/visibility 등은 수작업 불가
- 환경 다양성 부족: 도메인·조명·군중·노이즈 변수 제어가 현실 환경에서 불가능

---

## 5. 용어 정의 (요약)
- Scene: Factory/Office/Warehouse/Hospital/Military 등 환경
- Camera: 고정형 감시/현장 카메라 및 이동형 로봇 카메라
- Frame: 단일 이미지+라벨 세트
- Global ID: Scene/session 전체 ID, Track ID: 카메라별 연속 ID
- Session: 데이터 생성 실행 단위

---

## 6. 사용자 요구사항 (User Requirements)

### 6.0 우선순위 (MoSCoW)
| Priority | 포함 UR | 비고 |
|----------|---------|------|
| **Must** | UR-01, UR-02, UR-05, UR-07, UR-13 | Phase 1 필수 |
| **Should** | UR-09, UR-11, UR-12 | Phase 2 필수에 준함 |
| **Could** | UR-15, UR-23 | 상황에 따라 선택 |
| **Won't (이번 Phase)** | UR-41~UR-43 | Phase 4 Robotics 범위 |

### UR-01. Scene 다양성
최소 1개 Scene(Factory) 제공, Phase 2 이후 최소 3개 Scene 선택 가능.

### UR-02. Multi-camera 구성 가능
1~6대 카메라 설정(위치/방향/FOV/해상도/출력 포맷/ID, 고정/모바일, 경로/노이즈 포함).

### UR-02-1. 사용자 정의 Scene Asset 등록 (Phase 2+)
Unity 호환 Asset 업로드 + 메타데이터 검증 후 Scene Pool 반영.

### UR-03. 군중 규모 제어
Phase 1: 10~30명, Phase 2: 10~100명, Phase 3: 10~200명 이상.

### UR-04. 행동 패턴 선택
기본 Walk/Idle, Phase 2+ Group Move, Phase 3+ 넘어짐/싸움 등 선택적.

### UR-05. 자동 라벨 생성
2D bbox, Track ID, Global ID 자동 생성.

### UR-06. 고급 라벨 (Phase 2+)
Occlusion/Visibility, ReID crop export 옵션.

### UR-07. 대량 프레임 생성
Phase 1: ≥100k, Phase 2: ≥500k, Phase 3: ≥1M+ 프레임.

### UR-08. 세션 재현성
동일 Config → 동일 데이터셋 재생성.

### UR-09. Domain Randomization (Phase 2+)
조명/색감/노이즈 등 파라미터 제어(예: 밝기 0.2~1.8, σ 0.2 등).

(나머지 UR 항목은 기존 문서 구조를 유지하며 여기서 이어서 관리)

---

## 7. 추적성/연계
- 설계: 2_System_Architecture.md, 4_Data_Pipeline_Specification.md, 5_API Specification.md
- 데이터/스키마: 6_Database_Schema.md, common/0_datamodel_and_terminology.md
- 품질: 7_Test_and_Performance_Strategy.md, 8_Checkpoint_Mechanism.md
- UI/UX: UIUX/15~18번 문서
