#!/usr/bin/env bash
set -euo pipefail

SIM_ENDPOINT="${SIM_ENDPOINT:-http://localhost:8080/api/simulation/status}"
API_KEY_HEADER=""
AUTH_HEADER=""

if [ -n "${CCTV_SIM_API_KEY:-}" ]; then
  API_KEY_HEADER="-H X-Api-Key:${CCTV_SIM_API_KEY}"
fi

if [ -n "${CCTV_SIM_BEARER:-}" ]; then
  AUTH_HEADER="-H Authorization:Bearer ${CCTV_SIM_BEARER}"
fi

echo "[INFO] GET $SIM_ENDPOINT"
RESPONSE=$(curl -sS $API_KEY_HEADER $AUTH_HEADER "$SIM_ENDPOINT")

echo "$RESPONSE" | jq . >/dev/null || {
  echo "[ERROR] 유효한 JSON 응답이 아닙니다" >&2
  exit 1
}

ENGINE_VERSION=$(echo "$RESPONSE" | jq -r '.engineVersion // empty')
SUPPORTED=$(echo "$RESPONSE" | jq -r '.supportedVersions[]?')
AUTH_MODE=$(echo "$RESPONSE" | jq -r '.authMode // "unknown"')

if [ -z "$ENGINE_VERSION" ]; then
  echo "[WARN] engineVersion 필드가 응답에 없습니다"
else
  echo "[INFO] engineVersion=$ENGINE_VERSION"
fi

echo "[INFO] authMode=$AUTH_MODE"
if [ -n "$SUPPORTED" ]; then
  echo "[INFO] supportedVersions:"
  echo "$SUPPORTED" | sed 's/^/  - /'
else
  echo "[WARN] supportedVersions가 비어 있습니다"
fi

echo "[INFO] status_smoke 완료"
