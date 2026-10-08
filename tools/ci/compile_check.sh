#!/usr/bin/env bash
# Cloud compile check: compiles every project assembly (and the package assemblies it needs) with the real
# Unity 6000.3.25f1 reference assemblies, the editor's bundled Roslyn compiler and the project's csc.rsp,
# the same way the Unity editor on the owner's Mac does. No Unity license, no editor run.
#
# Usage:
#   tools/ci/compile_check.sh                 # all configurations (editor-ios, editor-osx, player-ios)
#   tools/ci/compile_check.sh editor-ios      # one or more configurations
#   tools/ci/compile_check.sh --fetch-only    # only download/extract the toolchain and packages
#   tools/ci/compile_check.sh --shaders       # also run the shader include check (tools/ci/shader_check.py)
#
# Environment:
#   JB_CI_CACHE         cache dir (default: $XDG_CACHE_HOME/junglebooze-ci or ~/.cache/junglebooze-ci)
#   JB_UNITY_TARBALL    use an already downloaded Unity Linux editor .tar.xz instead of streaming it
#
# See docs/ci/COMPILE_CHECK.md for what this covers and what it does not.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/UnityProject"

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt" | tr -d '[:space:]')"
UNITY_CHANGESET="$(sed -n 's/^m_EditorVersionWithRevision: .*(\(.*\))/\1/p' "$PROJECT/ProjectSettings/ProjectVersion.txt" | tr -d '[:space:]')"
CACHE="${JB_CI_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/junglebooze-ci}"
UNITY_DIR="$CACHE/unity-$UNITY_VERSION"
UNITY_URL="https://download.unity3d.com/download_unity/$UNITY_CHANGESET/LinuxEditorInstaller/Unity-$UNITY_VERSION.tar.xz"

# Built-in packages (shipped inside the editor) whose sources the check may compile or the shader check reads.
BUILTIN_SOURCE_PACKAGES=(
    com.unity.render-pipelines.core
    com.unity.render-pipelines.universal
    com.unity.render-pipelines.universal-config
    com.unity.shadergraph
    com.unity.ugui
    com.unity.test-framework
    com.unity.ext.nunit
)

log() { printf '[compile-check] %s\n' "$*" >&2; }

fetch_unity() {
    if [[ -f "$UNITY_DIR/.complete" ]]; then
        return
    fi
    log "Unity $UNITY_VERSION ($UNITY_CHANGESET) not cached; extracting the needed parts into $UNITY_DIR"
    rm -rf "$UNITY_DIR.partial"
    mkdir -p "$UNITY_DIR.partial"
    local bip="Editor/Data/Resources/PackageManager/BuiltInPackages"
    local patterns=(
        'Editor/Data/Managed/*'
        'Editor/Data/NetStandard/*'
        'Editor/Data/UnityReferenceAssemblies/*'
        'Editor/Data/NetCoreRuntime/*'
        'Editor/Data/DotNetSdkRoslyn/*'
        'Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/*'
        'Editor/Data/Tools/BuildPipeline/Compilation/*'
        "$bip/*/package.json"
    )
    local p
    for p in "${BUILTIN_SOURCE_PACKAGES[@]}"; do
        local ext
        for ext in cs asmdef asmref rsp dll dll.meta hlsl cginc shader; do
            patterns+=("$bip/$p/*.$ext")
        done
    done
    local start=$SECONDS
    if [[ -n "${JB_UNITY_TARBALL:-}" ]]; then
        log "Reading $JB_UNITY_TARBALL"
        tar -xJf "$JB_UNITY_TARBALL" -C "$UNITY_DIR.partial" --wildcards "${patterns[@]}"
    else
        log "Streaming $UNITY_URL (about 4.5 GB download, nothing large is stored; takes 5-10 minutes)"
        curl -fsSL --retry 5 --retry-all-errors "$UNITY_URL" \
            | tar -xJ -C "$UNITY_DIR.partial" --wildcards "${patterns[@]}"
    fi
    log "Extracted in $((SECONDS - start)) s ($(du -sh "$UNITY_DIR.partial" | cut -f1))"
    rm -rf "$UNITY_DIR"
    mv "$UNITY_DIR.partial" "$UNITY_DIR"
    touch "$UNITY_DIR/.complete"
}

main() {
    local fetch_only=0 shaders=0
    local configs=()
    local arg
    for arg in "$@"; do
        case "$arg" in
            --fetch-only) fetch_only=1 ;;
            --shaders) shaders=1 ;;
            -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
            *) configs+=("$arg") ;;
        esac
    done

    command -v python3 >/dev/null || { log "python3 is required"; exit 2; }
    command -v curl >/dev/null || { log "curl is required"; exit 2; }
    mkdir -p "$CACHE"
    fetch_unity
    # Registry packages (exact versions from packages-lock.json) are fetched by the Python driver.
    python3 -I "$SCRIPT_DIR/compile_check.py" --fetch-only \
        --project "$PROJECT" --unity "$UNITY_DIR" --cache "$CACHE" --unity-version "$UNITY_VERSION"
    if [[ $fetch_only -eq 1 ]]; then
        exit 0
    fi

    local status=0
    python3 -I "$SCRIPT_DIR/compile_check.py" \
        --project "$PROJECT" --unity "$UNITY_DIR" --cache "$CACHE" --unity-version "$UNITY_VERSION" \
        ${configs[@]+"${configs[@]}"} || status=$?
    if [[ $shaders -eq 1 ]]; then
        python3 -I "$SCRIPT_DIR/shader_check.py" --project "$PROJECT" --unity "$UNITY_DIR" || status=1
    fi
    exit $status
}

main "$@"
