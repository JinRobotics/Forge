## 1. 목적

본 문서는 Forge이 사용자의 관점에서 반드시 제공해야 하는 기능적 요구사항을 정의한다.  
모든 요구사항은 테스트·검증이 가능하도록 정량적 또는 명확한 형태로 표현한다.

---

## 2. 용어 정의

- **Scene**: Factory/Office/Warehouse/Hospital/Military 등 미리 정의된 환경
- **Camera**: 고정형 감시/현장 카메라 및 이동형 로봇 카메라
- **Frame**: 시뮬레이션 한 틱에서 생성되는 단일 이미지와 라벨 세트
- **Global ID**: Scene/session 전체에서 동일 인물을 식별하는 정수 ID
- **Track ID**: 카메라별 프레임 간 ID
- **Session**: 하나의 데이터 생성 실행 단위

---

## 3. 사용자 요구사항 (User Requirements)

### UR-01. Scene 다양성
시스템은 **최소 1개 이상의 Scene**(Factory)을 제공해야 한다.  
Phase 2 이후에는 **최소 3개 Scene**을 제공해야 하며,  
사용자는 생성 시 어느 Scene을 사용할지 선택할 수 있어야 한다.

---

### UR-02. Multi-camera 구성 가능
사용자는 **1~6대** 범위의 카메라를 생성 시 설정할 수 있어야 한다.  
각 카메라는 다음 속성을 설정 가능해야 한다:
- 위치/높이/방향
- 시야각(FOV)
- 해상도(예: 720p/1080p)  
- 출력 포맷(JPG/PNG)
- 카메라 고유 ID
- 카메라는 고정형 또는 이동형(로봇)으로 구성할 수 있으며, 이동형 카메라는 경로(waypoints)/속도/센서 노이즈(rolling shutter, motion blur)/시간 가변 extrinsic을 Config에서 정의해야 한다.

---

### UR-03. 군중 규모 제어
사용자는 생성 시 인물 수(최소/최대)를 지정할 수 있어야 한다.  
Phase별 목표:
- Phase 1: 10~30명  
- Phase 2: 10~100명  
- Phase 3: 10~200명 이상  

---

### UR-04. 행동 패턴 선택
사용자는 인물 행동 패턴을 선택/조합할 수 있어야 한다:
- 기본 행동: Walk, Idle  
- 확장 행동(Phase 2+): Group Move  
- 고급 행동(Phase 3+): 넘어짐/싸움/상호작용 등 (선택적)

---

### UR-05. 자동 라벨 생성
시스템은 **사람 단위 라벨**을 자동 생성해야 한다:
- 2D Bounding box  
- Track ID (카메라별)  
- Global ID (Scene/session 전체)  

※ 정확도·계산 방식은 후속 설계 산출물에서 구체화한다.

---

### UR-06. 고급 라벨(Phase 2+)
Phase 2부터 다음 라벨도 생성 가능해야 한다:
- Occlusion ratio  
- Visibility ratio  
- ReID용 crop 이미지 export 옵션

Phase 1에서는 필수 아님.

---

### UR-07. 대량 프레임 생성
시스템은 사용자 요청에 따라 다음 프레임 수를 생성할 수 있어야 한다:
- Phase 1: 최소 100,000 프레임  
- Phase 2: 최소 500,000 프레임  
- Phase 3: 최소 1,000,000+ 프레임  

---

### UR-08. 세션 재현성 (Reproducibility)
사용자는 하나의 Config 파일을 저장하고,
동일 Config로 생성할 경우 **동일한 데이터셋**을 복제할 수 있어야 한다.

---

### UR-09. Domain Randomization (Phase 2+)
사용자는 다음 항목을 세기/범위 기반으로 제어할 수 있어야 한다:
- 조명 (밝기/색온도)  
- 색감 (채도/대비/감마)  
- 카메라 노이즈 (Gaussian 등)  
- 날씨 효과 (비/안개 등)

Phase 1에서는 기본 조명만 제공하면 된다.

---

### UR-10. 실시간 진행률 확인
사용자는 데이터 생성 중 다음 정보를 실시간 확인할 수 있어야 한다:
- 현재 프레임 번호  
- FPS(처리 속도)  
- 예상 완료 시간  
- 현재 Scene / 활성 카메라 상태  
- 오류/경고 발생 여부 (off, warn, error)

업데이트 주기: 최소 1초 1회 이상

---

### UR-11. Export 포맷
사용자는 데이터를 다음 포맷으로 Export할 수 있어야 한다:
- 이미지: JPG 또는 PNG
- 라벨: JSON (기본),
  Phase 2+: YOLO / COCO
- Phase 2+: ReID 학습용 Dataset (person_id 별 crop 이미지)
- Phase 3+: Edge-friendly 포맷 (TFLite/ONNX 등)
- ReID Export 시 global_person_id, camera_id, frame_id, track_id, scene_name, bbox 메타데이터가 포함되어야 한다.
- 이동형 카메라 사용 시 frame별 extrinsic/pose 정보가 Export/manifest에 포함되어야 한다.

---

### UR-12. 데이터 품질 검증 결과 제공
세션이 완료되면 사용자는 자동 생성된 **검증 리포트**를 확인할 수 있어야 한다:
- 누락/손상 프레임 수  
- bbox/라벨 값 범위 검증 결과  
- 이미지-라벨 매칭 여부  
- 통계 요약 (사람 수, detection 수, occlusion histogram 등)  
표시 형식은 요구사항 수준에서 정의된 항목을 모두 포함해야 한다.

---

### UR-13. 동일 설정 반복 생성
사용자는 동일한 Config로 여러 번 실행하여  
동일한 dataset을 반복 생성할 수 있어야 한다 (seed 기반).

---

### UR-14. 주요 OS/GPU 환경 지원
사용자는 다음 환경에서 엔진을 사용할 수 있어야 한다:
- OS: Windows, Linux  
- GPU: NVIDIA 기반 환경  

지원 범위는 요구사항 수준에서 명시한 OS/GPU를 최소 기준으로 한다.

---

### UR-15. 윤리적 사용 가이드 제공
시스템은 사용자에게 다음을 포함한 윤리 가이드를 제공해야 한다:
- 합성 데이터임을 명시하는 방법  
- 실존 인물과 유사한 외형 생성 금지  
- 데이터 다양성 권고  
- 금지 용도 안내  

이 가이드는 문서 또는 UI에 제공되면 된다.

---

### UR-16. 품질/오류 정책 선택 (Phase 2+)
사용자는 Session 시작 전에 품질/오류 처리 정책을 선택할 수 있어야 한다:
- 품질 모드: strict / relaxed
- Back-pressure 시 동작: FPS 감속 / frame skip / pause / abort
- 오류 발생 시 정책: retry / skip / abort

---

### UR-17. Scene 시퀀스 설정
사용자는 Scene 순서와 Scene별 frame 범위(duration)를 Config에서 설정할 수 있어야 한다.

---

### UR-18. 세션 중단/재개 (Phase 2+)
사용자는 세션 중단 시 마지막 checkpoint부터 재개할 수 있어야 한다.

---

### UR-19. Config 검증 (Validation)
사용자는 Session 시작 전에 CLI/UI를 통해 Config가 스키마·값 범위·필수 필드 요건을 충족하는지 검증 결과를 확인할 수 있어야 한다.  
검증 실패 시 오류 메시지와 수정 포인트를 즉시 표시한다.

---

### UR-20. 세션 종료/중단 UX
사용자는 Session이 정상 종료/중단/실패한 경우를 명확히 구분해 확인할 수 있어야 한다.  
- 정상 종료: 완료 요약(프레임 수, FPS, 경고 카운트) 표시  
- 일시 중단/재개: 현재 상태와 재개 방법 안내  
- 실패: 원인(발생 Stage/오류 코드)과 재시도/재개 옵션 안내

---

### UR-21. Export 결과 구조 선택
사용자는 Export 시 디렉터리/파일 구성 스킴을 선택할 수 있어야 한다(예: 단일 Session 루트 vs 세션ID 기반 하위 디렉터리).  
선택한 스킴은 manifest에 기록되어야 한다.

---

### UR-22. 로그/진행 상태 저장 (Resume 친화)
사용자는 Session 진행 로그와 상태 요약을 파일로 저장하도록 설정할 수 있어야 하며,  
Resume 시 이 정보를 활용해 이어서 진행할 수 있어야 한다.

---

### UR-23. UI/CLI 사용성
- 실행 전 주요 프롬프트(출력 경로, config 확인, 품질/오류 정책 선택 등)를 제공한다.
- 실행 중 진행률/FPS/경고 요약을 1초 이상 주기로 표시한다.
- 실행 후 요약(프레임 수, 드롭/재시도/경고, 출력 경로, manifest 위치)을 제공한다.

---

### UR-24. 표준 `/metrics` 런타임 노출
시스템은 Prometheus 호환 `/metrics` 엔드포인트를 제공해야 하며, 다음 지표를 최소 1초 간격으로 노출해야 한다:
- FPS(현재/평균), Stage별 처리 FPS
- CPU/GPU 사용률 및 메모리 사용량
- 주요 Queue depth (Capture/Label/Encode 등)
- Stage별 처리 지연 시간(percentile 포함)

---

### UR-25. 세션 종료 성능 요약 리포트
세션이 종료되면 시스템은 다음 정보를 요약 리포트/manifest에 포함해야 한다:
- 평균/최소/최대 FPS
- Stage별 Max queue depth
- GPU/CPU peak usage, 메모리 peak
- Retry/Drop/GAP 카운트
- `/metrics` 스냅샷 경로 또는 참조

---

### UR-26. 로봇 카메라 Pose Export 및 Manifest
Multi-camera 구성이 로봇 카메라를 포함할 경우, 시스템은 다음을 Dataset/Manifest에 필수 포함해야 한다:
- Frame별 extrinsic(위치·회전), pose covariance
- 경로(waypoints), 속도 프로파일, SLAM 상태(유효/드리프트) 메타데이터
- ReID/Robotics/SLAM 연계를 위한 timestamp, camera_id, Sensor sync flag
- Pose 품질 지표(RMSE, drift) 및 누락 시 fallback 정책

---

## 4. 고급/선택 요구사항 (기업 운영 관점)
- strict/relaxed 품질 모드 노출 및 드롭/재시도 카운트 UI 제공
- Scene 시퀀스 프리뷰 및 예상 소요 시간 표시
- 오류 발생 시 정책 선택(continue/retry/abort)을 UI/CLI에서 직접 지정
- Liveness/Readiness 상태와 `/status` 요약 지표를 운영 대시보드(Grafana 등)로 확인 가능

---

## 5. Phase 4 Robotics 확장 요구사항

### UR-41. 센서 GT 동시 지원
Phase 4에서는 RGB 데이터와 함께 LiDAR, Depth, IMU, Odometry 센서 GT를 동시 생성·Export할 수 있어야 하며,
- 각 센서는 frame/timestamp 기준으로 RGB와 동기화돼야 하고
- Sensor 노이즈/캘리브레이션 파라미터를 Manifest에 포함해야 한다.

### UR-42. 로보틱스 포맷 지원
사용자는 Phase 4부터 다음 표준 포맷으로 Export를 선택할 수 있어야 한다:
- TUM RGB-D, KITTI Odometry/Raw
- 레이저 스캔/IMU를 포함한 Rosbag (선택)
- 포맷별 필수 필드 누락 시 검증 오류를 제공한다.

### UR-43. 센서 동기화 정확도
Robotics 세션에서 시스템은 RGB + 센서 스트림 동기화 정확도를 **±1ms 이내**로 유지해야 하며,
- 동기화 실패 시 경고 및 영향 구간을 리포트해야 한다.
