CCTV Synthetic Data Generation Engine

---

> **문서 버전:** v1.1 (2025-02-14)  
> **변경 이력:**  
> - v1.1 (2025-02-14): 문서 버전/변경 이력 섹션 추가, 인증/파이프라인 요구 최신화  
> - v1.0 (2024-12-01): 초기 작성

## 1. 문서 목적

본 문서는 CCTV Synthetic Data Generation Engine의 **기능적 요구사항(FR)** 과  
**비기능적 요구사항(NFR)** 을 개발자 관점에서 정의한다.

- **UR(사용자 요구)** 에서 정의된 WHAT을 기술적으로 실현하기 위한 REQUIREMENTS  
- “HOW”는 Architecture/LDD/Pipeline Spec 문서에서 정의한다  
- 모든 요구사항은 **테스트 가능**해야 한다

---

## 2. 범위(Scope)

본 시스템은 다음 기능을 수행한다:
- Multi-Scene 기반 시뮬레이션 실행
- Multi-Camera 이미지 생성
- 군중/행동/조명/랜덤화 제어
- 자동 라벨링(Detection/Tracking/Global ID/Embedding/Visibility 등)
- 병렬 데이터 파이프라인(Capture → Label → Encode → Storage)
- Validation/Statistics/Manifest 생성
- 다양한 학습/Edge-Friendly 포맷 Export

---

## 3. 정의 및 약어

- **Scene**: Factory/Office/Warehouse/Hospital/Military 등 독립 환경
- **Frame**: 이미지와 라벨을 포함하는 단일 단위
- **Session**: 하나의 시뮬레이션 실행 전체
- **Global ID**: Session 전체에서 동일 인물을 식별하는 ID
- **Track ID**: 카메라별 temporal ID
- **Config**: 사용자 설정(환경/카메라/군중/출력 옵션 포함)

---

# 4. Functional Requirements (FR)

SR에서는 다음 8개의 영역으로 구분한다.

1) Scene System  
2) Camera System  
3) Crowd/Behavior System  
4) Simulation Execution  
5) Labeling System  
6) Data Pipeline  
7) Validation / Statistics / Manifest  
8) Export System  

---

## 4.1 Scene System

### **FR-01. Scene 로딩**
시스템은 최소 **1개 이상의 Scene(Factory)** 을 로드할 수 있어야 한다.  
Phase 2 이상에서 **3개 이상 Scene**을 선택/교체할 수 있어야 한다.

### **FR-02. Scene 전환**
Phase 2부터, 사용자가 지정한 순서대로 Scene을 활성화할 수 있어야 한다.  
Scene 전환은 **Session 중간에 1회 이상** 발생할 수 있다.

### **FR-03. Scene 메타데이터 제공**
각 Scene은 다음 정보를 제공해야 한다:
- Scene 이름  
- 물리 좌표계 정보  
- 충돌/경로 정보(NavMesh)  
- 환경 정보(조명/시간대 등)

---

## 4.2 Camera System

### **FR-04. Multi-camera 활성화**
시스템은 **1~6대 카메라**를 동시에 활성화할 수 있어야 한다.

### **FR-05. 카메라 설정 반영**
카메라는 Config에서 정의된 다음 속성을 가져야 한다:
- 위치/회전  
- 해상도(예: 720p/1080p)  
- FOV  
- 출력 이미지 포맷  
- 카메라 고유 ID

### **FR-06. 카메라 메타데이터 제공**
각 카메라에 대해:
- intrinsic  
- extrinsic  
- 해상도  
- 카메라 ID  
를 라벨/manifest에 포함해야 한다.

---

## 4.3 Crowd & Behavior System

### **FR-07. Crowd 수 제어**
시스템은 Config에서 지정된 범위의 인원수를 Session 동안 유지해야 한다.

### **FR-08. 기본 행동 지원**
시스템은 최소 다음 행동을 시뮬레이션해야 한다:
- Walk  
- Idle  

### **FR-09. 확장 행동 지원 (Phase 2+)**
- Group Move  
- 속도/경로/패턴 편차  
- Appearance 다양화(의상/색상/신체 비율)

### **FR-10. 고급 행동 지원 (Phase 3+/선택)**
- 넘어짐/충돌/상호작용 등의 이벤트 기반 행동을 지원할 수 있어야 한다.

---

## 4.4 Simulation Execution

### **FR-11. 프레임 루프 실행**
시스템은 Frame 단위로 Simulation → Capture → Pipeline 실행을 반복해야 한다.

### **FR-12. 시간/조명 환경 설정**
사용자는 Config에서:
- 시간대(주간/야간/혼합)
- 밝기/색온도
를 설정할 수 있으며, 시스템은 이를 Simulation에 반영해야 한다.

### **FR-13. Domain Randomization (Phase 2+)**
시스템은 다음 랜덤화 옵션을 Simulation에 적용할 수 있어야 한다:
- 조명 랜덤화(밝기/색온도 범위)  
- 색감 랜덤화(채도/대비/감마)  
- 카메라 노이즈(강도/종류)  
- 날씨(비/안개 등)  

각 옵션의 강도는 Config 기반으로 결정된다.

---

## 4.5 Labeling System

### **FR-14. Bounding Box 생성**
각 프레임에서 사람 단위 bbox를 생성해야 한다.  
bbox는 이미지 해상도 안에 있어야 한다.

### **FR-15. Tracking ID**
Frame 간 동일 인물을 카메라 단위로 Track ID로 연결해야 한다.

### **FR-16. Global Person ID**
Scene/session 전체에서 동일 인물의 Global ID는 변하지 않아야 한다.

### **FR-17. ReID 학습용 Dataset Export (Phase 2+)**
시스템은 ReID 모델 학습에 필요한 person crop dataset을 export해야 한다.
Export 형식:
- person_id 별 디렉토리 구조
- 각 인물의 bbox crop 이미지
- camera_id 및 frame_id 메타데이터 포함

### **FR-18. Occlusion / Visibility (Phase 2+)**
시스템은 각 bbox에 대해:
- occlusion ratio (0~1)  
- visibility ratio (0~1)  
를 계산하여 라벨에 포함해야 한다.

---

## 4.6 Data Pipeline

### **FR-19. 파이프라인 단계**
시스템은 아래 5단계 파이프라인을 제공해야 한다:
1. Capture  
2. Label  
3. Encode  
4. Storage  
5. Post-Processing(Validation/Stats/Manifest)

### **FR-20. 비동기 처리 (Phase 2+)**
Phase 2부터 각 단계는 독립적인 Worker Queue 기반 비동기 처리여야 한다.

### **FR-21. 프레임 ID 관리**
각 프레임은 **Session 전체에서 유일한 frame_id**를 가져야 한다.

### **FR-22. camera_id 유지**
각 카메라는 Session 전체에서 고유 camera_id를 유지해야 한다.

---

## 4.7 Validation / Statistics / Manifest

### **FR-23. Technical Validation**
Session 종료 시 자동 Validation에 포함할 항목:
- 이미지 손상 여부  
- bbox 범위 유효성  
- 라벨-이미지 매칭 여부  
- 누락 프레임 여부  

### **FR-24. Statistics 생성**
Session 종료 시 기본 Statistics를 생성해야 한다:
- frame 수  
- detection 총 수  
- 인원 수  
- occlusion histogram  
- bbox scale histogram  

### **FR-25. manifest.json 생성**
Session 종료 시 다음 내용을 포함한 manifest.json을 생성해야 한다:
- SessionConfig  
- Scene 정보  
- Camera 정보  
- Statistics  
- Validation 결과  
- Output 정보  

---

## 4.8 Export System

### **FR-26. 이미지 Export**
이미지는 JPG/PNG로 저장해야 한다(사용자 선택).

### **FR-27. 라벨 Export**
Session 종료 후 다음 포맷 중 선택하여 export 가능해야 한다:
- JSON (기본)
- YOLO (Phase 2+)
- COCO (Phase 2+)

### **FR-28. Edge-NPU Export (Phase 3+)**
Edge 디바이스 용도를 위해 다음을 지원해야 한다:
- TFLite-compatible 포맷  
- ONNX 기반 경량 Export  
- Custom binary 라벨 구조  

---

# 5. Non-Functional Requirements (NFR)

## 5.1 Performance

### **NFR-01. 처리 FPS**
- Phase 1: 5~10 FPS (기본 파이프라인, 단일 스레드)
- Phase 2: 15~30 FPS (병렬 처리, Worker 기반)
- Phase 3: 30~60 FPS (최적화, Multi-GPU 고려)

근거:
- 100,000 프레임 @ 5 FPS = 5.5시간 (Phase 1 목표)
- 500,000 프레임 @ 20 FPS = 6.9시간 (Phase 2 목표)
- 1,000,000 프레임 @ 40 FPS = 6.9시간 (Phase 3 목표)
- 상업적 경쟁력: MOTSynth ~20 FPS, UnrealROX 10~15 FPS

### **NFR-02. Frame 규모**
- Phase 1: 100,000 프레임  
- Phase 2: 500,000 프레임  
- Phase 3: 1,000,000+ 프레임  

### **NFR-03. Storage 신뢰성**
- 파일 손상률 < 0.01% (100개/1M 프레임)
- 라벨-이미지 누락률 < 0.01%

근거:
- 0.1% = 1,000개/1M 프레임 손상 허용 (너무 관대함)
- 0.01% = 100개/1M 프레임 손상 허용 (허용 가능한 수준)  

---

## 5.2 Reliability / Stability

### **NFR-04. 장시간 실행 안정성**
시스템은 **12시간 이상** 연속 실행 시 중단 없이 작동해야 한다.

### **NFR-05. Checkpoint/Restart**
Session 도중 오류 발생 시
마지막 checkpoint 지점부터 재시작 가능해야 한다.(Phase 2+)

---

## 5.3 Maintainability

### **NFR-06. 모듈화**
Scene/Camera/Crowd/Label/Pipeline은 모듈화되어  
독립적으로 유지보수/확장 가능해야 한다.

### **NFR-07. Config 기반 실행**
코드 변경 없이 Config만으로 시나리오를 변경할 수 있어야 한다.

---

## 5.4 Compatibility

### **NFR-08. OS/GPU**
- 최소: Windows 10 이상, Ubuntu 20.04 이상  
- GPU: NVIDIA CUDA 기반 환경  
- Unity 2021 LTS 이상

---

## 5.5 Security / Privacy

### **NFR-09. 프라이버시 보장**
시스템은 실존 인물의 얼굴·신체·의상을 모사하지 않아야 한다.

### **NFR-10. 오프라인 실행**
데이터 생성은 인터넷 없이 **온프레미스 환경**에서도 가능해야 한다.

### **NFR-11. 데이터 격리 (Data Isolation)**
- 세션 간 데이터 격리 보장 (디렉토리/메모리 분리)
- 임시 파일 자동 삭제 (세션 종료 시)
- 민감 정보(경로, 사용자명 등) 로그에 기록 금지

### **NFR-12. 접근 제어 (Access Control)**
- 로컬 전용 실행 시 localhost만 바인딩 (0.0.0.0 금지)
- 분산 실행 시 mTLS 또는 API Key 필수
- 기본 포트 8080 (변경 가능)
