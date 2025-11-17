## 1. 목적

Scene Asset Registry는 UR-02-1 / FR-03-1에서 요구하는 “사용자 정의 Scene Asset 업로드 및 검증”의 단일 소스 정의다.  
본 문서는 Asset 업로드 → 검증 → Scene Pool 반영까지의 전체 흐름과 메타데이터 스키마, API 엔드포인트, 보안 정책을 규정한다.

---

## 2. 구성 요소

| 컴포넌트 | 역할 | 비고 |
|----------|------|------|
| `SceneAssetRegistry` Service | 업로드 요청 수신, 메타데이터 저장, 상태 관리 | Orchestration Layer |
| Validation Worker | 포맷/악성 코드 검사, NavMesh/조명 데이터 추출 | Sandbox 계정에서 실행 |
| `SceneMetadataStore` | 승인된 Scene 메타데이터(JSON) 저장소 | SQLite/PostgreSQL |
| Asset Storage | `assets/custom_scenes/{pending,ready,quarantine}` 디렉터리 | Security Doc §4.4 |

---

## 3. 워크플로우

1. **업로드 요청**  
   - `POST /scene-assets/upload` (API Spec §3)  
   - 멀티파트 폼: `assetFile`, `metadata.json`, `sceneName`, `version`, `uploader`.
   - 업로드 완료 후 파일은 `assets/custom_scenes/pending/{sceneName}/{version}/`에 저장.
2. **메타데이터 검증**  
   - `metadata.json`은 아래 스키마를 따르고, 필수 필드 누락 시 즉시 `status=rejected`.
3. **포맷/보안 검사**  
   - 허용 확장자(.fbx/.obj/.unitypackage/AssetBundle) 확인  
   - 압축 해제 후 크기(기본 10GB 이하), 파일 개수(10,000개 이하) 검사  
   - ClamAV 및 Unity Import Validator 실행
4. **Scene Feature 추출**  
   - Unity headless 툴이 Asset을 임포트해 NavMesh, Light Probe, Collider Layer, 좌표계 등을 수집.  
   - 결과를 `SceneMetadataStore`에 저장하고 `metadata.json`에 merge.
5. **승인/배포**  
   - 검증 성공 시 파일을 `ready/`로 이동하고 `SceneCatalog`에 추가.  
   - 실패 시 `quarantine/`으로 이동 후 30일 후 자동 삭제. Diagnostics에 상세 사유 기록.
6. **Config 반영**  
   - `SceneConfig`에서 `type: "custom"` 과 `sceneAssetId`를 입력하면 Orchestration이 해당 메타데이터를 로드한다.  
   - Manifest에는 `sceneAssets[]` 섹션으로 업로드 소스/버전을 기록한다.

---

### 3.1 Asset Validation Pipeline

| 단계 | 설명 | 실패 처리 |
|------|------|-----------|
| 1. Malware Scan | ClamAV 최신 시그니처로 `pending/scene/version` 전체를 검사. 압축 파일은 샌드박스에서 해제 후 재검사. | 즉시 `quarantine/` 이동, `scene_asset_audit.log`에 해시/사유 기록 |
| 2. Format Validation | Unity Headless 프로세스에서 `AssetDatabase.LoadAssetAtPath`로 파일 로드, 허용 확장자/용량/파일 개수 검증. | 실패 리포트 + `quarantine/` 이동 |
| 3. Metadata Integrity Check | `metadata.json`을 JSON Schema로 검증하고 좌표계/단위/NavMesh/Lighting 필드를 추출. | HTTP 400 응답과 함께 오류 목록 반환 |
| 4. Sandbox Test Load | 제한 권한 Worker가 별도 Unity 인스턴스에서 Scene을 Additive 로드, NavMesh/LightProbe/Collider를 추출하고 Pose 테스트 실행. | 실패 시 Diagnostics에 상세 로그 첨부, `quarantine/` 이동 |
| 5. Approval & Catalog | 모든 단계 통과 시 `ready/` 이동, `SceneMetadataStore.validation.status=ready`로 갱신 후 Config 옵션에 노출. | - |

- 각 단계는 독립 Queue Worker로 실행되며, `/audit/scene-assets` API는 단계별 타임스탬프/결과를 제공한다.
- Security & Compliance 문서 §4.4의 격리/삭제 정책을 그대로 준수하며, 실패 자산은 30일 후 자동 삭제 cron job(`scene-asset-cleaner`)이 처리한다.

---

## 4. 메타데이터 스키마

```json
{
  "$schema": "scene-asset-metadata-v1.json",
  "sceneName": "Factory_Custom_A",
  "version": "2024.03",
  "uploader": "user@example.com",
  "units": "meter",
  "coordinateSystem": "left-handed",
  "navMesh": {
    "available": true,
    "areas": ["Walkable", "Restricted"],
    "bakeResolution": 0.1
  },
  "lightingPresets": ["day", "night"],
  "timeOfDayRange": ["06:00", "22:00"],
  "collisionLayers": ["Default", "Obstacles"],
  "assetPaths": {
    "bundle": "assets/custom_scenes/ready/Factory_Custom_A/2024.03/scene.bundle"
  },
  "validation": {
    "status": "ready",
    "checkedAt": "2024-03-01T10:00:00Z",
    "toolVersion": "forge-validator 1.2.0",
    "issues": []
  }
}
```

필드 설명:

- `sceneName`, `version`: Scene Pool 키 구성 요소 (유일).  
- `units`, `coordinateSystem`: Orchestration이 카메라/사람 배치를 환산할 때 사용.  
- `navMesh`/`lightingPresets`/`timeOfDayRange`: ScenarioManager가 Scene 전환 시 적용.  
- `assetPaths.bundle`: 실제 AssetBundle 경로 (Sandbox 내).  
- `validation.status`: `pending`/`ready`/`quarantine`. Manifest에도 동일 값 기록.

정식 JSON Schema는 `docs/design/schema/scene-asset-metadata.schema.json`(추후 추가)에서 관리한다.

---

## 5. API/CLI 연계

- **API**: `POST /scene-assets/upload`, `GET /scene-assets/{sceneName}/status`, `GET /scene-assets/catalog`.  
- **CLI**: `forge scene upload --file Scene.unitypackage --metadata metadata.json`.  
- **Config**: `scene: { type: "custom", assetId: "Factory_Custom_A:2024.03" }`.

모든 호출은 `docs/design/common/terminology.md`의 명칭을 사용하고, Manifest는 `manifest.sceneAssets[]` 배열을 통해 결과를 노출한다.

---

## 6. 보안 & 감사

- 업로드는 Role=`admin`/`operator`만 허용, mTLS/API Key 필수.  
- Worker는 제한 권한 계정으로 실행하며, 검증 실패 파일은 `quarantine/`으로 이동 후 30일 뒤 삭제.  
- `scene_asset_audit.log`에 업로더, 해시, 상태 전환 기록을 남기고, `/audit/scene-assets` API로 조회 가능.
