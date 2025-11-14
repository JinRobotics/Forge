#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SOLUTION_NAME="CCTVSim"

command -v dotnet >/dev/null 2>&1 || {
  echo "[ERROR] dotnet CLI가 설치되어 있지 않습니다. https://dotnet.microsoft.com/ 에서 SDK를 설치한 뒤 다시 실행하세요." >&2
  exit 1
}

cd "$ROOT_DIR/src"

if [ ! -f "$SOLUTION_NAME.sln" ]; then
  echo "[INFO] dotnet solution 생성: $SOLUTION_NAME.sln"
  dotnet new sln -n "$SOLUTION_NAME"
else
  echo "[INFO] 기존 $SOLUTION_NAME.sln 감지"
fi

PROJECTS=(
  "CCTVSim.Application"
  "CCTVSim.Orchestration"
  "CCTVSim.Simulation"
  "CCTVSim.DataPipeline"
  "CCTVSim.Services"
  "CCTVSim.DataModel"
)

for project in "${PROJECTS[@]}"; do
  if [ ! -d "$project" ]; then
    echo "[INFO] dotnet new classlib -o $project"
    dotnet new classlib -n "$project" -o "$project"
  fi
  dotnet sln "$SOLUTION_NAME.sln" list | grep -q "$project/$project.csproj" || {
    echo "[INFO] 솔루션에 $project 추가"
    dotnet sln "$SOLUTION_NAME.sln" add "$project/$project.csproj"
  }
done

# 프로젝트 간 참조 연결

dotnet add CCTVSim.Application/CCTVSim.Application.csproj reference \
  CCTVSim.Orchestration/CCTVSim.Orchestration.csproj \
  CCTVSim.Services/CCTVSim.Services.csproj

dotnet add CCTVSim.Orchestration/CCTVSim.Orchestration.csproj reference \
  CCTVSim.Simulation/CCTVSim.Simulation.csproj \
  CCTVSim.DataPipeline/CCTVSim.DataPipeline.csproj \
  CCTVSim.DataModel/CCTVSim.DataModel.csproj \
  CCTVSim.Services/CCTVSim.Services.csproj

dotnet add CCTVSim.Simulation/CCTVSim.Simulation.csproj reference CCTVSim.DataModel/CCTVSim.DataModel.csproj

dotnet add CCTVSim.DataPipeline/CCTVSim.DataPipeline.csproj reference CCTVSim.DataModel/CCTVSim.DataModel.csproj CCTVSim.Services/CCTVSim.Services.csproj

dotnet add CCTVSim.Services/CCTVSim.Services.csproj reference CCTVSim.DataModel/CCTVSim.DataModel.csproj

cat <<'MANIFEST' > Directory.Build.props
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
MANIFEST

echo "[DONE] 솔루션 초기화 완료"
