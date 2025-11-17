## 1. 목적

Forge API의 버전 관리 정책을 정의하여 Breaking Change를 예측 가능하게 하고, Orchestration · Simulation · Worker · 외부 클라이언트 간 호환성을 보장한다.

---

## 2. 버전 체계

- **형식**: `v{MAJOR}.{MINOR}.{PATCH}` (`/api/v1/` 경로는 Major 버전)
- **의미**
  - **Major**: 하위 호환이 깨지는 변경. 새로운 경로 또는 기존 필드 제거.
  - **Minor**: 하위 호환 기능 추가. 필드 추가, 선택 파라미터 확장.
  - **Patch**: 버그 수정, 문서/스키마 정정. 동작은 동일.
- REST 응답 헤더:
  - `X-Engine-Version`: `1.4.0`
  - `X-Api-Version`: `v1.2.0`
  - `Supported-Versions`: `["v1","v1beta"]`
- 클라이언트 요청 헤더:
  - `Accept-Version: v1` (없으면 최신 안정 버전 사용)

---

## 3. Deprecation 정책

| 변경 유형 | 사전 공지 | 지원 기간 | 요구 조치 |
|-----------|-----------|-----------|-----------|
| Major | 1 release(최소 3개월) 전에 `Deprecated: true` 헤더 및 changelog 공지 | 최소 12개월 | 새 버전으로 마이그레이션, 이전 버전은 `sunsetDate`에 제거 |
| Minor | 릴리즈 노트에 즉시 공지 | 1 release cycle | 선택 필드 추가이므로 기존 클라이언트 영향 없음 |
| Patch | 필요 시 즉시 | 즉시 | API 동작 동일 |

- `/status` 응답에 `deprecations[]` 값을 추가하여 곧 제거될 버전을 노출한다.
- Deprecated 버전은 `/api/v1beta` 형태로 1년간 병행 운영 후 `410 Gone` 응답으로 전환한다.

---

## 4. 변경 관리 프로세스

1. **RFC 제출** (`docs/rfcs/API-XXXX.md`):
   - 변경 이유, 영향 범위, 마이그레이션 전략 포함.
2. **스키마 업데이트**:
   - `docs/design/schema/*.schema.json`과 `API Specification` 문서를 동시에 갱신.
3. **Backward Compatibility Test**:
   - `tests/integration/ApiCompatibilityTests.cs`에서 이전 버전 Contract fixture 실행.
4. **CI 게이트**:
   - `.github/workflows/docs-validation.yml`에서 `DocumentationContract` 카테고리가 schema diff를 검증.
5. **릴리즈 노트**:
   - `CHANGELOG.md`에 Deprecated/Added/Removed 항목 명시, sunset 날짜 포함.

---

## 5. 테스트 및 문서 요구사항

- 각 REST 엔드포인트는 `docs/design/5_API Specification.md`와 동기화된 `status.schema.json`, `manifest.schema.json` 등 단일 소스 스키마를 가져야 한다.
- Major 변경 시:
  - `tests/contracts/v{N-1}/` fixture를 유지하며, 새로운 버전을 `tests/contracts/vN/`으로 추가.
  - `ApiVersionMatrix.md`에 지원 조합(클라이언트 ↔ 서버)을 명시.
- Minor 변경 시:
  - 기본값과 선택 필드를 명시한 예제 요청/응답을 문서에 추가하고, `openapi.yaml`을 재생성한다.

---

## 6. 역할과 책임

| 역할 | 책임 |
|------|------|
| API Owner | RFC 승인, sunset 일정 관리 |
| Tech Writer | API Spec 및 Versioning Policy 업데이트 |
| QA 팀 | Backward compatibility 테스트와 CI 게이트 운영 |
| Developer | 코드 변경 전에 Versioning Policy 준수 여부 체크리스트 작성 |

---

## 7. 레퍼런스

- `docs/design/5_API Specification.md`
- `docs/design/schema/manifest.schema.json`
- `.github/workflows/docs-validation.yml`
