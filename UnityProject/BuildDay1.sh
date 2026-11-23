#!/bin/bash
set -euo pipefail

project_dir="$(cd "$(dirname "$0")" && pwd -P)"
editor_path="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/6000.0.85f2/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$editor_path" ]]; then
    printf 'Unity Editor not found at %s\n' "$editor_path" >&2
    exit 1
fi

case "${1:-both}" in
    tracking) build_targets=(TrackingCheck) ;;
    interaction) build_targets=(InteractionCheck) ;;
    both) build_targets=(TrackingCheck InteractionCheck) ;;
    *) printf 'Usage: %s [tracking|interaction|both]\n' "$0" >&2; exit 1 ;;
esac

# Unity's Android tools require an ASCII-only project path.
staging_dir="$(mktemp -d /private/tmp/warehouse-vr.XXXXXX)"
trap 'rm -rf -- "$staging_dir"' EXIT
cp -R "$project_dir/Assets" "$project_dir/Packages" "$project_dir/ProjectSettings" "$staging_dir/"
mkdir -p "$project_dir/Builds" "$project_dir/Logs"

cd "$staging_dir"
for target in "${build_targets[@]}"; do
    log_path="$project_dir/Logs/Build-$target.log"
    printf 'Building %s. Log at %s\n' "$target" "$log_path"
    if ! "$editor_path" -batchmode -nographics -quit -buildTarget Android \
            -projectPath "$staging_dir" -executeMethod "Warehouse.Editor.PicoAndroidBuild.Build$target" \
            -logFile "$log_path"; then
        tail -n 40 "$log_path" >&2
        exit 1
    fi

    case "$target" in
        TrackingCheck) apk=Warehouse-Tracking.apk ;;
        InteractionCheck) apk=Warehouse-Day1.apk ;;
    esac
    mv -f "$staging_dir/Builds/$apk" "$project_dir/Builds/$apk"
    printf 'APK built at %s\n' "$project_dir/Builds/$apk"
done
