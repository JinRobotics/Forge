# 기획 문서 종합 검토 보고서

> **작성일**: 2025-02-14
> **대상 문서**:
> - 0_Concept_Document.md
> - 1_User_Requirements.md
> - 1_System_Requirements.md

---

## 1. 개요

본 보고서는 Forge 프로젝트의 기획 문서(Concept, User Requirements, System Requirements)를 종합 검토하여
**문서 간 일관성**, **요구사항 추적성**, **설계 문서와의 정합성**을 평가한다.

---

## 2. 검토 결과 요약

| 분류 | 발견 건수 | 심각도 |
|------|-----------|--------|
| **요구사항 누락** | 4건 | 🔴 높음 |
| **문서 간 불일치** | 3건 | 🟡 중간 |
| **모순/불명확** | 3건 | 🟡 중간 |
| **Phase 범위 불일치** | 2건 | 🟢 낮음 |
| **성능 목표 검증 필요** | 1건 | 🟢 낮음 |

**총 발견 건수**: 13건

---

## 3. 상세 발견사항

### 3.1 요구사항 누락 (Critical) 🔴

#### **Issue #1: ReID Dataset Export 기능 설계 누락**

**심각도**: 🔴 높음

**위치**:
- 1_User_Requirements.md (UR-11): "Phase 2+: ReID 학습용 Dataset (person_id 별 crop 이미지)"
- 1_System_Requirements.md (FR-17): "ReID 모델 학습에 필요한 person crop dataset을 export해야 한다"

**문제**:
- 3_Class_Design_Document.md에 **ReIDExportWorker 클래스가 없음**
- 4_Data_Pipeline_Specification.md에 **ReID Export 파이프라인 단계가 없음**
- Section 6 데이터 모델에도 ReID export 관련 구조가 없음

**영향**:
- Phase 2 핵심 요구사항이 설계에 반영되지 않음
- ReID 연구자 페르소나(Concept 1.1)의 핵심 니즈가 충족되지 않음

**권장 조치**:
```
1. 3_Class_Design_Document.md에 다음 추가 필요:
   - Section 7.9: ReIDExportWorker 클래스 정의
   - Section 6.11: ReIDExportJob 데이터 모델 정의

2. 4_Data_Pipeline_Specification.md에 다음 추가 필요:
   - Section 4.7: ReIDExportWorker 파이프라인 사양
   - PersonCrop 추출 로직, 디렉토리 구조(FastReID/TorchReID 호환)
```

---

#### **Issue #2: Domain Randomization 세부 구현 미정의**

**심각도**: 🔴 높음

**위치**:
- 1_System_Requirements.md (FR-13): "조명/색감/카메라 노이즈/날씨 랜덤화를 Simulation에 적용"

**문제**:
- 2_System_Architecture.md Section 2.5에 "Randomization Module (Phase 2)" 언급만 있음
- 구체적인 랜덤화 알고리즘, 파라미터 범위, Unity 구현 방법이 없음
- SessionConfig에 랜덤화 관련 필드 정의 없음

**영향**:
- Phase 2 핵심 기능인 Sim-to-Real 성능 향상 전략이 구현 불가
- UC-03 (Sim-to-Real 연구 실험 설계)을 수행할 수 없음

**권장 조치**:
```
1. 2_System_Architecture.md Section 2.5 확장:
   - RandomizationConfig 클래스 정의
   - 조명 랜덤화: IntensityRange, ColorTempRange
   - 색감 랜덤화: SaturationRange, ContrastRange, GammaRange
   - 노이즈: GaussianNoise, SaltPepperNoise 파라미터
   - 날씨: Rain/Fog 강도, 적용 확률

2. 3_Class_Design_Document.md에 다음 추가:
   - Section 5.5: RandomizationModule 클래스
   - Unity PostProcessing Volume 기반 구현 전략
```

---

#### **Issue #3: Occlusion/Visibility 계산 로직 미정의**

**심각도**: 🟡 중간

**위치**:
- 1_System_Requirements.md (FR-18): "occlusion ratio, visibility ratio를 계산하여 라벨에 포함"

**문제**:
- 3_Class_Design_Document.md Section 6.6 DetectionData에 필드는 정의됨:
  ```csharp
  public float occlusionRatio;  // 0~1
  public float visibilityRatio; // 0~1
  ```
- 하지만 **계산 알고리즘이 LabelWorker나 다른 곳에 명시되지 않음**
- "어떻게 계산하는가?"에 대한 설계가 없음

**영향**:
- Phase 2 고급 라벨링 기능 구현 시 개발자가 독자적으로 알고리즘을 고안해야 함
- 계산 방식에 따라 성능/정확도가 크게 달라질 수 있음

**권장 조치**:
```
1. 3_Class_Design_Document.md Section 7.3 LabelWorker에 추가:
   - CalculateOcclusion() 메서드 사양
   - Raycast 기반 또는 Stencil Buffer 기반 접근법 선택

2. 또는 별도 Section 5.6: OcclusionCalculator 클래스 정의:
   - 3D bbox → 2D projection → visible pixel ratio 계산 로직
   - Unity의 Graphics.Blit + Shader 기반 구현 전략
```

---

#### **Issue #4: Edge-NPU Export 설계 누락**

**심각도**: 🟢 낮음 (Phase 3 기능이므로 현재는 낮은 우선순위)

**위치**:
- 1_System_Requirements.md (FR-28): "TFLite/ONNX/Custom binary 라벨 구조 지원"

**문제**:
- 설계 문서 어디에도 Edge Export 관련 클래스/모듈이 없음

**권장 조치**:
```
Phase 3 착수 전에:
1. 3_Class_Design_Document.md에 EdgeExportWorker 추가
2. TFLite/ONNX 포맷 변환 로직 정의
```

---

### 3.2 문서 간 불일치 🟡

#### **Issue #5: Embedding 용어 정의 불일치**

**심각도**: 🟡 중간

**위치**:
- 1_System_Requirements.md (2. 정의 및 약어): "Embedding: Appearance feature vector (차원은 고정 아님)"

**문제**:
- 0_Concept_Document.md에 Embedding 언급 없음
- 1_User_Requirements.md에도 Embedding 언급 없음
- **SR에서 갑자기 등장한 용어**
- 설계 문서에도 Embedding 추출/저장 기능 없음

**분석**:
- Embedding은 ReID 모델 학습의 핵심 개념이므로 추가된 것으로 보임
- 하지만 Concept/UR과의 정합성이 없어 요구사항 추적이 불가능

**권장 조치**:
```
Option 1 (권장): Embedding 기능을 UR-11에 추가
  - "ReID 학습용 Dataset에는 person crop 이미지와 함께
     Appearance Embedding을 포함할 수 있다"

Option 2: SR에서 Embedding 정의 삭제
  - 현재 설계에 반영되지 않았으므로 혼란 방지 차원에서 제거
```

---

#### **Issue #6: Scene 전환 메커니즘 불명확**

**심각도**: 🟡 중간

**위치**:
- 1_System_Requirements.md (FR-02): "Session 중간에 1회 이상 Scene 전환 발생 가능"

**문제**:
- 2_System_Architecture.md에 SceneManager는 있으나 **Session 실행 중 전환 로직 없음**
- SessionConfig에 Scene 순서/전환 타이밍 정의 없음
- 3_Class_Design_Document.md Section 5.2 SceneManager에도 런타임 전환 메서드 없음

**영향**:
- UC-02 (Multi-Scene 구성: Office + Warehouse)를 구현할 수 없음
- FR-02 요구사항을 만족하는 설계가 없음

**권장 조치**:
```
1. SessionConfig에 추가:
   - public List<SceneTransition> sceneSequence;
   - class SceneTransition { string sceneName; int atFrame; }

2. Section 5.2 SceneManager에 추가:
   - SwitchScene(string sceneName) 메서드
   - UnloadScene(string sceneName), LoadScene(string sceneName) 비동기 처리

3. Section 4.1 GenerationController에 추가:
   - 프레임 루프 중 sceneSequence 체크 로직
   - Scene 전환 시 PersonState 마이그레이션 로직
```

---

#### **Issue #7: Robot 카메라 지원 범위 불명확**

**심각도**: 🟢 낮음

**위치**:
- 0_Concept_Document.md (4.2): "고정형 CCTV + (선택적) Robot 이동형 카메라"
- Phase 2 범위: "이동형 Robot 카메라 지원 (기본 구현)"

**문제**:
- UR/SR 어디에도 Robot 카메라 요구사항 없음
- 설계 문서는 고정형 카메라만 고려

**분석**:
- Concept에서 "선택적"으로 언급했으나 실제 요구사항으로 구체화되지 않음
- Phase 2 범위에 포함되어 있으나 UR/SR 추적이 불가능

**권장 조치**:
```
Option 1: Robot 카메라를 Phase 3 선택 기능으로 재분류
Option 2: UR-02에 Robot 카메라 요구사항 명시 추가
  - "카메라는 고정형(CCTV) 또는 이동형(Robot)을 선택할 수 있다"
```

---

### 3.3 모순/불명확 사항 🟡

#### **Issue #8: Global ID 관리 - Scene 전환 시 유지 메커니즘 불명확**

**심각도**: 🟡 중간

**위치**:
- 1_System_Requirements.md (FR-16): "Scene/session 전체에서 동일 인물의 Global ID는 변하지 않아야"

**문제**:
- 3_Class_Design_Document.md Section 8.1에 IDRangeAllocator는 있음
- 하지만 **Scene 전환 시 Global ID 유지 방법이 명시되지 않음**
  - Scene A에서 Person ID=5였던 인물이 Scene B로 이동 시 어떻게 추적?
  - PersonState를 Scene 간 마이그레이션하는 로직이 없음

**영향**:
- Multi-Scene Session에서 Cross-scene ReID가 불가능
- UC-02의 핵심 가치(Global ID + Appearance Feature)를 구현할 수 없음

**권장 조치**:
```
1. Section 5.2 SceneManager에 추가:
   - MigratePersonStates(Scene from, Scene to) 메서드
   - Scene 전환 시 활성 PersonState를 새 Scene으로 이관

2. Section 8.1 IDRangeAllocator에 추가:
   - Global ID는 Session 전체에서 유일하며 Scene에 독립적임을 명시

3. PersonState에 추가:
   - public string currentScene; // 현재 어느 Scene에 있는지 추적
```

---

#### **Issue #9: YOLO/COCO Export 로직 설계 누락**

**심각도**: 🟡 중간

**위치**:
- 1_User_Requirements.md (UR-11): "Phase 2+: YOLO / COCO"
- 1_System_Requirements.md (FR-27): "YOLO (Phase 2+), COCO (Phase 2+)"

**문제**:
- 현재 설계 문서에 YOLO/COCO Export 클래스/로직이 없음
- 3_Class_Design_Document.md에 ExportWorker 자체가 정의되지 않음

**권장 조치**:
```
1. 3_Class_Design_Document.md에 추가:
   - Section 7.10: ExportWorker 클래스
   - ConvertToYOLO(), ConvertToCOCO() 메서드

2. 4_Data_Pipeline_Specification.md에 추가:
   - Section 4.8: ExportWorker 사양
   - YOLO format: <class> <x_center> <y_center> <width> <height> (normalized)
   - COCO format: JSON annotations, categories, images 구조
```

---

#### **Issue #10: Checkpoint 파일 포맷 불명확**

**심각도**: 🟢 낮음

**위치**:
- 9_Checkpoint_Mechanism.md: Checkpoint 복구 프로세스는 상세하나 **파일 포맷 미정의**

**문제**:
- CheckpointData 클래스는 있으나 직렬화 포맷이 명시되지 않음
  - JSON? Binary? Protobuf?
- 파일 확장자, 네이밍 규칙도 불명확

**권장 조치**:
```
9_Checkpoint_Mechanism.md Section 3에 추가:
- 파일 포맷: JSON (가독성 우선) 또는 Binary (성능 우선)
- 네이밍: checkpoint_{sessionId}_{frameNumber}.json
- 예시 JSON 구조 추가
```

---

### 3.4 Phase별 범위 정합성 문제 🟢

#### **Issue #11: Checkpoint 기능 Phase 표기 일관성**

**심각도**: 🟢 낮음 (실제로는 일관됨)

**위치**:
- 0_Concept_Document.md (Phase 2 범위): "Checkpoint 기반 세션 재시작"
- 1_System_Requirements.md (NFR-05): "Phase 2+"
- 9_Checkpoint_Mechanism.md: 상세 설계 존재

**분석**:
- Phase 2에 포함되는 것으로 일관됨
- "Phase 2+"는 "Phase 2부터 시작하여 Phase 3에서도 유지"를 의미하므로 모순 없음

**조치**: 불필요 (False Positive)

---

#### **Issue #12: AsyncGPUReadback Phase 표기 차이**

**심각도**: 🟢 낮음

**위치**:
- 0_Concept_Document.md (Phase 1 제외): "AsyncGPUReadback"은 Phase 2부터
- 2_System_Architecture.md: 현재 설계에 AsyncGPUReadback 반영됨

**분석**:
- 설계 문서가 Phase 2 이상을 대비하여 작성된 것으로 보임
- Phase 1에서는 ReadPixels 사용 예정이나 설계에는 AsyncGPUReadback으로 작성
- 혼선 가능성 있음

**권장 조치**:
```
2_System_Architecture.md Section 3.1 또는 3_Class_Design_Document.md에:
- Phase 1: ReadPixels (동기식)
- Phase 2+: AsyncGPUReadback (비동기)
를 명시적으로 구분 표기
```

---

### 3.5 성능 목표 검증 필요 🟢

#### **Issue #13: Phase 3 FPS 목표의 현실성 검토**

**심각도**: 🟢 낮음

**위치**:
- 1_System_Requirements.md (NFR-01):
  - Phase 3: 30~60 FPS (최적화, Multi-GPU 고려)
  - 근거: MOTSynth ~20 FPS, UnrealROX 10~15 FPS

**분석**:
- Phase 3 목표 30~60 FPS는 경쟁 제품 대비 **2~6배 성능**
- Multi-GPU 활용을 전제로 하나, 구체적인 병렬화 전략이 설계되지 않음
- "최적화"만으로 2~6배 향상은 비현실적일 수 있음

**권장 조치**:
```
1. NFR-01 재검토:
   - Phase 3: 20~40 FPS (현실적 목표로 조정)
   - 또는 Multi-GPU 구체적 전략 수립 후 목표 유지

2. 8_Performance_Benchmarks.md에 다음 추가:
   - GPU 병렬화 전략 (Multi-camera를 Multi-GPU에 분산)
   - 예상 성능 향상 수치 (2-GPU: 1.8배, 4-GPU: 3.2배 등)
```

---

## 4. 우선순위별 조치 권장사항

### 🔴 높음 (Phase 2 착수 전 필수)

1. **ReID Dataset Export 기능 설계 추가** (Issue #1)
   - 3_Class_Design_Document.md Section 7.9, 6.11 추가
   - 4_Data_Pipeline_Specification.md Section 4.7 추가

2. **Domain Randomization 상세 설계 추가** (Issue #2)
   - 2_System_Architecture.md Section 2.5 확장
   - RandomizationConfig, RandomizationModule 정의

### 🟡 중간 (Phase 2 개발 중 해결)

3. **Occlusion/Visibility 계산 로직 정의** (Issue #3)
   - LabelWorker 또는 OcclusionCalculator 클래스 추가

4. **Scene 전환 메커니즘 설계** (Issue #6)
   - SessionConfig, SceneManager, GenerationController 업데이트

5. **Global ID Scene 마이그레이션 로직** (Issue #8)
   - SceneManager.MigratePersonStates() 추가

6. **YOLO/COCO Export 클래스 추가** (Issue #9)
   - ExportWorker 설계 및 포맷 변환 로직

7. **Embedding 용어 정리** (Issue #5)
   - UR-11에 추가 또는 SR에서 삭제

### 🟢 낮음 (Phase 3 또는 선택적)

8. **Edge-NPU Export 설계** (Issue #4)
   - Phase 3 착수 시 추가

9. **Robot 카메라 범위 정리** (Issue #7)
   - Phase 분류 재조정

10. **Checkpoint 파일 포맷 명시** (Issue #10)
    - 9_Checkpoint_Mechanism.md 보완

11. **AsyncGPUReadback Phase 구분 표기** (Issue #12)
    - 설계 문서에 Phase별 구현 차이 명시

12. **Phase 3 FPS 목표 재검토** (Issue #13)
    - 현실적 목표로 조정 또는 Multi-GPU 전략 구체화

---

## 5. 문서 추적성 매트릭스

| Concept Use Case | User Requirement | System Requirement | 설계 문서 반영 | 상태 |
|------------------|------------------|--------------------|----------------|------|
| UC-01 (공장 안전감시) | UR-01, UR-05, UR-07 | FR-01, FR-14~FR-16 | ✅ 반영 | ✅ |
| UC-02 (Cross-camera ReID) | UR-11 (ReID Dataset) | FR-17 | ❌ **누락** | 🔴 |
| UC-03 (Sim-to-Real) | UR-09 (Randomization) | FR-13 | ⚠️ 부분 반영 | 🟡 |
| Phase 2 (Multi-Scene) | UR-01 (3개 Scene) | FR-02 (Scene 전환) | ⚠️ 메커니즘 불명확 | 🟡 |
| Phase 2 (고급 라벨) | UR-06 | FR-18 (Occlusion) | ⚠️ 계산 로직 없음 | 🟡 |
| Phase 2 (YOLO/COCO) | UR-11 | FR-27 | ❌ **누락** | 🔴 |
| Phase 3 (Edge Export) | UR-11 | FR-28 | ❌ 누락 (Phase 3) | 🟢 |

---

## 6. 종합 평가

### 6.1 강점
- **Concept → UR → SR 흐름이 명확함**: 페르소나, Use Case 기반 요구사항 도출이 체계적
- **Phase별 범위가 잘 정의됨**: 점진적 확장 전략이 현실적
- **NFR 정량화**: FPS, Frame 규모, 손상률 등 측정 가능한 목표 제시

### 6.2 약점
- **설계 문서와의 정합성 부족**: UR/SR에서 요구한 기능(ReID Export, Randomization, YOLO/COCO Export)이 설계에 누락
- **Traceability 단절**: Concept의 핵심 Use Case(UC-02, UC-03)를 구현할 설계가 불완전
- **메커니즘 미정의**: Scene 전환, Global ID 마이그레이션 등 복잡한 기능의 HOW가 빠짐

### 6.3 리스크
- Phase 2 착수 시 **핵심 기능(ReID Export, Randomization) 재설계 필요** → 일정 지연 가능성
- 설계 누락 발견이 개발 중 발생 시 **아키텍처 변경 비용 증가**

---

## 7. 결론 및 권장사항

### 7.1 단기 조치 (Phase 1 완료 전)
1. ✅ **설계 문서 업데이트**:
   - Issue #1, #2 (ReID Export, Randomization) 설계 추가
   - 3_Class_Design_Document.md, 4_Data_Pipeline_Specification.md 수정

2. ✅ **요구사항 정합성 검증**:
   - UR/SR에 명시된 모든 기능이 설계에 반영되었는지 체크리스트 작성

### 7.2 중기 조치 (Phase 2 착수 전)
3. ✅ **Traceability Matrix 유지**:
   - 본 보고서 Section 5와 같은 추적 매트릭스를 프로젝트 관리 도구에 통합

4. ✅ **Architecture Review**:
   - Scene 전환, Global ID 마이그레이션 등 복잡한 기능의 설계 검토 회의 수행

### 7.3 장기 조치
5. ✅ **문서 버전 관리**:
   - Concept/UR/SR 변경 시 설계 문서 동기화 프로세스 수립

6. ✅ **성능 목표 재검토**:
   - Phase 3 FPS 목표를 실제 Phase 1/2 성능 측정 후 조정

---

**보고서 종료**
