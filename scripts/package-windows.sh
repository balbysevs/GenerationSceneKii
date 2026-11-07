#!/bin/bash
set -euo pipefail

task_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
task_output="${1:-$task_root/artifacts/windows}"
mkdir -p "$task_output"
task_output="$(cd "$task_output" && pwd)"
task_archive="$task_output/SceneGen-win-x64.zip"
if [[ -L "$task_archive" || -d "$task_archive" ]]; then
    echo "Не удалось подготовить путь для ZIP: $task_archive" >&2
    exit 1
fi

task_stage="$(mktemp -d "$task_output/.scenegen-build.XXXXXX")"
trap 'rm -rf "$task_stage"' EXIT

dotnet publish "$task_root/SceneGenerator.Desktop/SceneGenerator.Desktop.csproj" \
    -c Release -r win-x64 --self-contained true \
    -p:UseAppHost=true -p:PublishTrimmed=false -p:PublishSingleFile=false \
    -p:DebugType=None -p:DebugSymbols=false \
    --disable-build-servers --artifacts-path "$task_root/artifacts/build/windows/win-x64" \
    -o "$task_stage/SceneGen"

find "$task_stage/SceneGen" -type f -name '*.pdb' -delete
mv "$task_stage/SceneGen/SceneGenerator.Desktop.exe" "$task_stage/SceneGen/SceneGen.exe"
cp "$task_root/packaging/windows/README.txt" "$task_stage/SceneGen/README.txt"
(
    cd "$task_stage"
    zip -q -r SceneGen-win-x64.zip SceneGen
    unzip -tq SceneGen-win-x64.zip
)
mv "$task_stage/SceneGen-win-x64.zip" "$task_archive"
echo "Готово: $task_archive"
