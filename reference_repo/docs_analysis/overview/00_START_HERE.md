# 여기서 시작: Unity Perception 분석

CCTV Synthetic Data 프로젝트를 위한 Unity Perception 패키지 분석 문서입니다.

---

## 📚 문서 구성

**한글 문서** (메인):

1. **README.md** - 네비게이션 가이드
   - 문서 구조 및 역할별 읽기 경로
   - 핵심 요약

2. **1_빠른시작.md** (10분) - 핵심 개념 이해
   - Unity Perception이란?
   - 우리 프로젝트와의 관계
   - 핵심 개념 3가지
   - 3단계 통합 계획

3. **2_아키텍처_개요.md** (30분) - 상세 아키텍처
   - 5대 핵심 컴포넌트
   - CCTV 프로젝트 적합성 평가
   - 채택해야 할 패턴

4. **3_개발자_참조.md** (1-2시간) - 개발 가이드
   - Labeler 개발 가이드 (코드 예제)
   - Randomizer 개발 가이드 (코드 예제)
   - 핵심 파일 참조

**참조 자료** (reference/):
- 디렉토리_구조.txt - 100+ 파일 전체 맵
- 파일_경로.txt - 절대 경로 목록
- 영문 원본 문서들 (INDEX, README, ANALYSIS 등)

---

## 🎯 역할별 추천 경로

### PM / 의사결정자 (10분)
```
README.md (개요)
  → 1_빠른시작.md (핵심 개념)
```

### 아키텍트 (1시간)
```
1_빠른시작.md
  → 2_아키텍처_개요.md
    → 평가 및 의사결정
```

### Unity 개발자 - Labeler 담당 (3시간)
```
1_빠른시작.md
  → 2_아키텍처_개요.md
    → 3_개발자_참조.md (Section 1: Labeler)
      → reference_repo/com.unity.perception/
```

### Unity 개발자 - Randomizer 담당 (3시간)
```
1_빠른시작.md
  → 2_아키텍처_개요.md
    → 3_개발자_참조.md (Section 2: Randomizer)
      → reference_repo/com.unity.perception/
```

---

## ⚡ 핵심 요약

### Unity Perception이란?
합성 데이터 생성 툴킷 - Ground Truth 라벨링, Domain Randomization, JSON 출력 지원. Unity가 Computer Vision 연구용으로 개발 (현재 커뮤니티 유지보수).

### CCTV 프로젝트 핵심 가치
- **Pluggable Labeler 시스템**: 코어 수정 없이 커스텀 라벨 추가
- **파라미터 기반 Randomization**: 설정 파일로 씬 변화 제어
- **Async Future**: GPU 작업 비동기 처리
- **Clean Architecture**: 데이터 모델, Labeler, Randomizer, 출력 분리

### 재사용 가능한 것
- ✅ PerceptionCamera 컴포넌트
- ✅ Labeler/Randomizer 프레임워크
- ✅ Parameter/Sampler 시스템
- ✅ Tag Manager (동적 GameObject 쿼리)
- ✅ JSON 데이터셋 구조
- ✅ AsyncFuture 패턴

### 직접 구현할 것
- ❌ CCTV 특화 Labelers (PersonDetection, CrowdDensity 등)
- ❌ CCTV 특화 Randomizers (노이즈, 행동 패턴)
- ❌ CCTV 센서 (어안 렌즈, PTZ)
- ❌ 출력 포맷 커스터마이징 (YOLO, COCO)
- ❌ Multi-camera 동기화 (6대)
- ❌ 스트리밍/실시간 캡처

---

## 🏗️ 아키텍처 다이어그램

```
SCENARIO (생명주기 관리)
  └─ RANDOMIZERS (씬 변화)
     ├─ Parameters (설정 값)
     └─ Samplers (난수 생성)
        └─ Query Tagged GameObjects

PERCEPTION CAMERA (캡처)
  └─ LABELERS (라벨링)
     └─ AsyncFuture<Annotation>
        └─ DatasetCapture
           └─ IConsumerEndpoint (출력)
```

---

## 📂 파일 위치

```
lk_sdg/
├── README.md                           # 프로젝트 개요
├── docs/
│   └── analysis/
│       ├── 00_START_HERE.md            # 👈 여기
│       ├── README.md                   # 네비게이션
│       ├── 1_빠른시작.md                # 10분 개요
│       ├── 2_아키텍처_개요.md           # 30분 상세
│       ├── 3_개발자_참조.md             # 1-2시간 개발 가이드
│       └── reference/                  # 참조 자료
│           ├── 디렉토리_구조.txt
│           ├── 파일_경로.txt
│           └── (영문 원본 문서들)
│
└── reference_repo/
    └── com.unity.perception/           # 소스 코드
```

---

## 🚀 다음 단계

**1. 빠른 개요** (10분):
- [README.md](README.md) 읽기
- [1_빠른시작.md](1_빠른시작.md) 읽기

**2. 상세 분석** (30분):
- [2_아키텍처_개요.md](2_아키텍처_개요.md) 읽기

**3. 개발 시작** (1-2시간):
- [3_개발자_참조.md](3_개발자_참조.md) 읽기
- reference_repo/com.unity.perception/ 코드 읽기

**4. 파일 찾기**:
- [reference/디렉토리_구조.txt](reference/디렉토리_구조.txt) - 100+ 파일 맵
- [reference/파일_경로.txt](reference/파일_경로.txt) - 절대 경로 목록

---

**분석 생성일**: 2025-11-14
**분석 대상**: Unity Perception (discontinued/community-maintained)
**목적**: CCTV Synthetic Data Generation System
**분석 범위**: 100+ runtime 파일, 15 labelers, 9+ randomizers, 3+ output endpoints

---

**👉 지금 읽기: [README.md](README.md)**
