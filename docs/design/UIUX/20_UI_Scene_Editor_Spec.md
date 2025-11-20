본 문서는 Forge Phase 2 기능인 **Scene Editor**의 UI/UX 세부 스펙을 정의한다.  
Scene Editor는 SceneMetadata, CameraMeta, CrowdConfig 등을 시각적으로 편집하기 위한 독립 화면이며,  
Simulation Layer와 Config Layer의 데이터 구조를 사용자 친화적으로 조작할 수 있게 한다.

---

# 1. 목적 (Purpose)

Scene Editor는 다음을 목표로 한다:

- 카메라 배치(CameraMeta.extrinsic)의 시각 탐색 및 조정
    
- 카메라 FOV, 해상도, 방향(회전) 시각화
    
- Crowd 초기 스폰 영역 및 밀도 설정
    
- SceneMetadata(Asset Registry) 기반 Floorplan 확인
    
- 수정 결과를 SessionConfig에 반영
    

이 기능은 일반 UI와 달리, **시각 편집기(Editor)**에 가까운 고유 UI이다.

---

# 2. 데이터 모델 매핑 (DataModel Mapping)

Scene Editor는 다음 데이터를 기반으로 작동한다:

- **CameraMeta**
    
    - intrinsic
        
    - extrinsic (position, rotation)
        
    - resolution
        
    - fov
        
- **SceneMetadata** (Scene Asset Registry)
    
- **CrowdConfig**
    
- **SessionConfig.scenes[]**
    
- **SessionConfig.cameras[]**
    

이 모든 데이터 구조는 datamodel 및 System Requirements 문서와 정합성을 유지한다.

---

# 3. 주요 기능 (Features)

## 3.1 Scene Floorplan 시각화

- SceneMetadata의 좌표계 기반 Floorplan 렌더링
    
- 2D top-view 또는 3D simplified view
    

## 3.2 카메라 배치 편집

- Camera 아이콘 위치 표시
    
- 드래그로 extrinsic.position 변경
    
- Rotation handle로 extrinsic.rotation 변경
    
- FOV cone 표시(fov 기반)
    
- Camera 선택 시 속성 편집 패널 활성화
    

## 3.3 카메라 속성 패널

선택된 CameraMeta의 속성을 표시·수정

- cameraId
    
- position (x, y, z)
    
- rotation (pitch, yaw, roll)
    
- resolution
    
- fov
    

## 3.4 Crowd 설정

- Crowd spawn zone 박스 표시(드래그로 크기/위치 조절)
    
- density, min/max agent 설정
    

## 3.5 Scene Metadata 표시

- sceneName
    
- navmesh 여부 표시
    
- asset validation 상태 표시
    

---

# 4. 화면 구조 (Wireframe)

```
[Scene Editor]
 -----------------------------------------------------------
 |                       Floorplan                          |
 |  [cam01] → FOV cone                                      |
 |  [cam02] → FOV cone                                      |
 |  [Crowd spawn zone]                                      |
 -----------------------------------------------------------

[Right Panel – Camera Properties]
 Selected: cam01
  • Position: (x, y, z)
  • Rotation: (pitch, yaw, roll)
  • Resolution
  • FOV
 [Apply Changes]
 [Reset]

[Bottom Panel – Crowd Config]
 • Density
 • Spawn Zones
 • Agent Min/Max
```

---

# 5. Interaction Rules

## Camera

- 드래그 → extrinsic.position 변경
    
- 회전 핸들 → extrinsic.rotation 변경
    
- FOV slider → fov 변경
    

## Crowd

- Spawn zone 박스 드래그로 영역 변경
    
- density slider 조정
    

## 저장 규칙

- 변경사항은 SessionConfig.cameras[] / SessionConfig.crowd에 반영
    
- 저장 전 diff 표시 가능
    

---

# 6. Phase 연계

- Phase 2 핵심 기능으로 UI Navigation에 Scenes 메뉴 활성화
    
- Phase 3/4와 충돌 없음
    
- Robotics Viewer와 floorplan 정보를 공유할 수 있음
    

---