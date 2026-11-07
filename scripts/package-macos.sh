#!/bin/bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin ]]; then
    echo "Сборка .app с локальной подписью выполняется на macOS." >&2
    exit 1
fi

task_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
case "${1:-$(uname -m)}" in
    arm64|osx-arm64) task_rid=osx-arm64 ;;
    x86_64|osx-x64) task_rid=osx-x64 ;;
    *) echo "Укажите osx-arm64 или osx-x64." >&2; exit 1 ;;
esac

task_output="${2:-$task_root/artifacts/macos/$task_rid}"
mkdir -p "$task_output"
task_output="$(cd "$task_output" && pwd)"
task_app="$task_output/SceneGen.app"
if [[ -L "$task_app" ]]; then
    echo "Нельзя заменять символическую ссылку: $task_app" >&2
    exit 1
fi
if [[ -e "$task_app" ]] && [[ "$(/usr/libexec/PlistBuddy -c 'Print CFBundleIdentifier' "$task_app/Contents/Info.plist" 2>/dev/null || true)" != ru.scenegen.desktop ]]; then
    echo "В папке назначения уже есть другое приложение. Укажите другую папку." >&2
    exit 1
fi

task_stage="$(mktemp -d "$task_output/.scenegen-build.XXXXXX")"
cleanup() {
    if [[ -d "$task_stage/previous.app" && ! -e "$task_app" ]]; then
        mv "$task_stage/previous.app" "$task_app"
    fi
    rm -rf "$task_stage"
}
trap cleanup EXIT

dotnet publish "$task_root/SceneGenerator.Desktop/SceneGenerator.Desktop.csproj" \
    -c Release -r "$task_rid" --self-contained true \
    -p:UseAppHost=true -p:PublishTrimmed=false -p:PublishSingleFile=false \
    -p:DebugType=None -p:DebugSymbols=false \
    --disable-build-servers --artifacts-path "$task_root/artifacts/build/macos/$task_rid" \
    -o "$task_stage/SceneGen.app/Contents/MacOS"

mkdir -p "$task_stage/SceneGen.app/Contents/Resources"
cp "$task_root/packaging/macos/Info.plist" "$task_stage/SceneGen.app/Contents/Info.plist"
cp "$task_root/SceneGenerator.Desktop/Assets/SceneGen.icns" "$task_stage/SceneGen.app/Contents/Resources/SceneGen.icns"
printf 'APPL????' > "$task_stage/SceneGen.app/Contents/PkgInfo"
chmod +x "$task_stage/SceneGen.app/Contents/MacOS/SceneGenerator.Desktop"
/usr/bin/plutil -lint "$task_stage/SceneGen.app/Contents/Info.plist"

while IFS= read -r -d '' task_binary; do
    /usr/bin/codesign --force --timestamp=none --sign - "$task_binary"
done < <(find "$task_stage/SceneGen.app/Contents/MacOS" -type f ! -name SceneGenerator.Desktop -print0)

/usr/bin/codesign --force --timestamp=none \
    --entitlements "$task_root/packaging/macos/Entitlements.plist" \
    --sign - "$task_stage/SceneGen.app"
/usr/bin/codesign --verify --deep --strict "$task_stage/SceneGen.app"

if [[ -d "$task_app" ]]; then mv "$task_app" "$task_stage/previous.app"; fi
mv "$task_stage/SceneGen.app" "$task_app"
echo "Готово: $task_app"
