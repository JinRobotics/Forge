본 문서는 Forge Phase 4 기능인 **Robotics Trajectory Viewer** UI/UX의 세부 스펙을 정의한다.  
이 화면은 Isaac Sim 기반 로봇 센서/궤적 Ground Truth 데이터(robotPose, SensorQuality 등)를 시각적으로 분석하기 위한 독립 기능으로,  
Session Detail 화면에 포함되지 않는 별도의 고급 분석 툴이다.

---

# 1. 목적 (Purpose)

Robotics Trajectory Viewer는 다음을 목표로 한다:

- 로봇 이동 경로(trajectory) 시각화
    
- 센서 데이터 품질(sensorQuality) 분석
    
- sync drift, pose missing, 누락 프레임 등 품질 이상 탐지
    
- timestamp/frame scrubber 기반 탐색 기능 제공
    
- position/rotation/velocity/time-series 그래프 제공
    

이 기능은 일반 UI가 아닌 **전용 분석 뷰어(Viewer)**이다.

---

# 2. 데이터 모델 매핑 (DataModel Mapping)

- **FrameContext.sensors.robotPose**
    
    - position (x, y, z)
        
    - rotation (quaternion)
        
    - timestamp
        
- **SensorQuality**
    
    - latency
        
    - sync_offset_ms
        
    - isValid
        
    - errorMessage
        
- **Trajectory Export Formats**
    
    - TUM
        
    - KITTI
        
- **SceneMetadata** (floorplan 연계)
    

---

# 3. 주요 기능 (Features)

## 3.1 로봇 Trajectory Overlay

- Scene floorplan 위에 robotPose를 polyline으로 그림
    
- keyframe pose는 별도 마커로 표시 (예: 0.5초 단위)
    
- drift 발생 시 포인트를 강조(빨간색 또는 아이콘)
    

## 3.2 Timeline Scrubber

- frameId 또는 timestamp 기반 슬라이더
    
- scrub 시 모든 뷰(overlay + 그래프)가 동기 업데이트
    

## 3.3 Sensor Graph Panel

- position(x,y,z) vs time
    
- rotation(yaw) vs time
    
- velocity vs time
    
- sensor drift(sync_offset_ms) vs time
    

## 3.4 Pose Inspector

특정 포즈 선택 시 상세 정보 표시:

- position/rotation
    
- timestamp
    
- robot velocity
    
- sensorQuality breakdown
    

---

# 4. 화면 구조 (Wireframe)

```
[Trajectory Viewer]
 -------------------------------------------------------------
 |                         Floorplan                          |
 |   • Robot Path (polyline)                                  |
 |   • Keyframe pose markers                                   |
 |   • Drift warning markers                                   |
 -------------------------------------------------------------

[Timeline Scrubber]
 [-----■-----------------------------]
 Frame: 12,345      Timestamp: 123.456

[Sensor Graphs]
 - position(x,y,z) graph
 - yaw graph
 - velocity graph
 - drift graph

[Pose Inspector]
 Frame: 12345
 Pos: (3.2, 1.1, 0.0)
 Rot(yaw): 91°
 Drift: 4.2ms
```

---

# 5. Interaction Rules

## 이동

- Scrubber 이동 → 모든 뷰 sync
    
- Floorplan에서 pose 클릭 → scrubber 해당 위치로 이동
    

## 하이라이트

- drift 이상 구간은 빨간색으로 강조
    
- sensor invalid 시 경고 아이콘 표시
    

## Hover

- polyline hover 시 해당 pose의 timestamp 표시
    

## Zoom/Pan

- Floorplan 시각화 영역은 기본 zoom/pan 지원
    
- Graph들도 zoom 가능
    

---

# 6. Phase 연계

- Phase 4에서 활성화되는 기능
    
- Scene Editor의 floorplan과 시각적 자원을 공유할 수 있음
    
- Worker/Session UI와 독립된 고급 분석 모드
    

---

# 7. 확장 계획 (Optional)

- Multi-robot overlay
    
- SLAM trajectory 비교 (GT vs estimated)
    
- LiDAR 2D/3D point overlay
    
- IMU/odom 분석 패널 추가
    

---