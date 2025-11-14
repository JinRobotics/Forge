# Design Documents - Overview

CCTV Synthetic Data Generation Engine의 설계 문서 모음입니다.

---

## 📋 문서 목록

### 🏗️ Core Design Documents

| # | 문서명 | 설명 | 상태 |
|---|--------|------|------|
| 0 | [Repo Structure](0_Repo%20Structure.md) | 프로젝트 디렉토리 구조 | ✅ Complete |
| 1 | [System Requirements](1_System_Requirements.md) | 기능적/비기능적 요구사항 (FR/NFR) | ✅ Updated |
| 2 | [System Architecture](2_System_Architecture.md) | 4-Layer 아키텍처 설계 | ✅ Complete |
| 3 | [Class Design](3_Class_Design_Document.md) | 클래스 구조 및 관계 | ✅ Updated |
| 4 | [Data Pipeline Spec](4_Data_Pipeline_Specification.md) | 파이프라인 상세 설계 | ✅ Updated |
| 5 | [API Specification](5.%20API%20Specification.md) | REST API 및 Config 명세 | ✅ Updated |

### 🔧 Implementation Guides

| # | 문서명 | 설명 | Phase |
|---|--------|------|-------|
| 6 | [Test Strategy](6_Test_Strategy.md) | 테스트 전략 (단위/통합/E2E/성능) | ✅ Phase 1+ |
| 7 | [Performance Benchmarks](7_Performance_Benchmarks.md) | 성능 목표 및 벤치마크 시나리오 | ✅ Phase 1+ |
| 8 | [Checkpoint Mechanism](8_Checkpoint_Mechanism.md) | Checkpoint/Recovery 설계 | ⏳ Phase 2+ |
| 9 | [Security & Compliance](9_Security_and_Compliance.md) | 보안 및 규정 준수 | ✅ All Phases |

### 📊 Diagrams

| 다이어그램 | 위치 | 설명 |
|-----------|------|------|
| System Architecture | [2_System_Architecture.md](2_System_Architecture.md#시스템-아키텍처-다이어그램) | 전체 시스템 구조 (Mermaid) |
| Class Diagram | [3_Class_Design_Document.md](3_Class_Design_Document.md#클래스-다이어그램) | 핵심 클래스 관계 (Mermaid) |
| Sequence Diagram | [4_Data_Pipeline_Specification.md](4_Data_Pipeline_Specification.md#23-frame-generation-sequence-diagram) | Frame 생성 흐름 (Mermaid) |

**다이어그램 형식**: Mermaid 형식으로 문서 내 코드블럭에 포함됨 (GitHub/GitLab에서 자동 렌더링)

---

## 🎯 주요 변경 사항 (최근 업데이트)

### 1. NFR 목표 재설정
- **FPS 목표 상향**:
  - Phase 1: 5~10 FPS (기존 1~5)
  - Phase 2: 15~30 FPS (기존 5~15)
  - Phase 3: 30~60 FPS (기존 10~20)
- **근거**: 상업적 경쟁력, 프레임 생성 시간 현실화

### 2. 손상률 기준 강화
- **파일 손상률**: < 0.01% (기존 0.1%)
- **근거**: 1M 프레임 기준 1,000개 손상 허용 → 100개로 강화

### 3. 보안 요구사항 추가
- **NFR-11**: 데이터 격리 (세션 간, 임시 파일, 로그)
- **NFR-12**: 접근 제어 (localhost 바인딩, mTLS)

### 4. ReID 기능 재정의
- **변경 전**: ReID Embedding 생성 (512-dim vector)
- **변경 후**: ReID 학습용 Dataset Export (person crop)
- **이유**: 실제 사용 목적과 일치, 성능 부담 감소

---

## 📖 읽기 순서 (권장)

### 처음 읽는 경우
1. **System Requirements** (1) → 무엇을 만들 것인가?
2. **System Architecture** (2) → 어떻게 구성할 것인가?
3. **Diagrams** → 시각적 이해
4. **Data Pipeline Spec** (4) → 핵심 로직 상세

### 구현 시작 전
1. **Class Design** (3) → 클래스 구조 파악
2. **API Specification** (5) → 인터페이스 정의
3. **Test Strategy** (6) → 테스트 계획
4. **Performance Benchmarks** (7) → 성능 목표

### Phase별 추가 문서
- **Phase 1**: 문서 1~7
- **Phase 2**: + Checkpoint Mechanism (8)
- **Phase 3**: 모든 문서 + 추가 최적화 문서 (추후 작성)

---

## 🔍 문서 간 참조 관계

```
Concept (0_Concept_Document.md)
  └─> User Requirements (1_User_Requirements.md)
       └─> System Requirements (1_System_Requirements.md)
            ├─> System Architecture (2_System_Architecture.md)
            │    ├─> Class Design (3_Class_Design_Document.md)
            │    └─> Data Pipeline Spec (4_Data_Pipeline_Specification.md)
            ├─> API Specification (5. API Specification.md)
            ├─> Test Strategy (6_Test_Strategy.md)
            ├─> Performance Benchmarks (7_Performance_Benchmarks.md)
            ├─> Checkpoint Mechanism (8_Checkpoint_Mechanism.md)
            └─> Security & Compliance (9_Security_and_Compliance.md)
```

---

## ✅ 문서 품질 체크리스트

### 필수 항목
- [x] 모든 FR/NFR 정의
- [x] 아키텍처 다이어그램 제공
- [x] 클래스/인터페이스 정의
- [x] API 명세 (OpenAPI는 향후 추가)
- [x] 테스트 전략
- [x] 성능 목표
- [x] 보안 가이드

### 추가 권장 (Phase 2+)
- [ ] OpenAPI (Swagger) 스펙
- [ ] 운영 가이드 (Monitoring, Logging)
- [ ] Deployment 가이드 (Docker, Kubernetes)
- [ ] 데이터셋 품질 메트릭 상세

---

## 🛠️ 개발 착수 전 필수 확인사항

### 설계 검토
1. NFR 목표가 현실적인가? (FPS, 메모리, 디스크)
2. 아키텍처 다이어그램 이해했는가?
3. 주요 클래스 책임 분리가 명확한가?

### 환경 준비
1. Unity 2021 LTS 설치
2. .NET 6.0 이상 설치
3. GPU 환경 확인 (NVIDIA CUDA)
4. JSON Schema 검증 도구 설정

### 도구 및 라이브러리
1. PlantUML (다이어그램 렌더링)
2. xUnit/NUnit (테스트)
3. BenchmarkDotNet (성능 측정)
4. Prometheus + Grafana (모니터링)

---

## 📝 문서 업데이트 가이드

### 문서 수정 시
1. 관련 문서들도 함께 업데이트
2. 변경 이력 기록 (이 README 또는 CHANGELOG)
3. 다이어그램 수정 시 `.puml` + 생성 이미지 모두 커밋

### 새 문서 추가 시
1. 이 README에 항목 추가
2. 적절한 번호 부여 (10, 11, ...)
3. 문서 간 참조 관계 업데이트

### 문서 리뷰 절차
1. 기술 리뷰 (아키텍처, 구현 가능성)
2. 문서 품질 리뷰 (가독성, 일관성)
3. 승인 후 main 브랜치 merge

---

## 🔗 추가 리소스

### 내부 문서
- [Concept Documents](../concept/) - 기획 및 사용자 요구사항
- [README](../../README.md) - 프로젝트 개요

### 외부 참고
- [Unity Documentation](https://docs.unity3d.com/)
- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [PlantUML](https://plantuml.com/)
- [JSON Schema](https://json-schema.org/)

---

## 📞 문의

**설계 문의**: architecture@cctvsim.io (가상)
**구현 지원**: dev@cctvsim.io (가상)
**문서 개선 제안**: GitHub Issues

---

**Last Updated**: 2024-01-XX (문서 업데이트 날짜)
**Version**: 2.0 (embedding 재정의, NFR 업데이트 반영)
