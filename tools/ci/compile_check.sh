#!/usr/bin/env bash
# Cloud compile check: compiles every project assembly (and the package assemblies it needs) with the real
# Unity 6000.3.25f1 reference assemblies, the editor's bundled Roslyn compiler and the project's csc.rsp,
# the same way the Unity editor on the owner's Mac does. No Unity license, no editor run.
#
# Usage:
#   tools/ci/compile_check.sh                 # C#: editor-ios, editor-osx, player-ios; then the shader check
#   tools/ci/compile_check.sh editor-ios      # only the listed C# configurations (plus the shader check)
#   tools/ci/compile_check.sh --no-shaders    # skip the shader check (tools/ci/shader_check.py)
#   tools/ci/compile_check.sh --fetch-only    # only download/extract the toolchain and packages
#
# Exit status: 0 when everything compiles without errors or warnings, 1 otherwise, 2 for setup problems.
#
# Environment:
#   JB_CI_CACHE         cache dir (default: $XDG_CACHE_HOME/junglebooze-ci or ~/.cache/junglebooze-ci)
#   JB_UNITY_TARBALL    use an already downloaded Unity Linux editor .tar.xz instead of streaming it
#   JB_IOS_TARBALL      same for the Linux iOS Build Support .tar.xz
#   JB_VULKAN_SDK_TARBALL  same for the LunarG Vulkan SDK .tar.xz (source of the DXC shader compiler)
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
IOS_DIR="$CACHE/unity-$UNITY_VERSION-ios"
IOS_URL="https://download.unity3d.com/download_unity/$UNITY_CHANGESET/LinuxEditorTargetInstaller/UnitySetup-iOS-Support-for-Editor-$UNITY_VERSION.tar.xz"
# DXC (Microsoft's open-source HLSL compiler, the one Unity uses for Metal) from a pinned LunarG Vulkan SDK.
VULKAN_SDK_VERSION="1.4.363.0"
DXC_DIR="$CACHE/dxc-$VULKAN_SDK_VERSION"
VULKAN_SDK_URL="https://sdk.lunarg.com/sdk/download/$VULKAN_SDK_VERSION/linux/vulkan-sdk.tar.xz"

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
        'Editor/Data/NetCoreRuntime/*'
        'Editor/Data/DotNetSdkRoslyn/*'
        'Editor/Data/Tools/BuildPipeline/Unity.SourceGenerators/*'
        "$bip/*/package.json"
    )
    local p
    for p in "${BUILTIN_SOURCE_PACKAGES[@]}"; do
        patterns+=("$bip/$p/*")
    done
    # Package content the check never reads (samples, docs, images, models, assets).
    local excludes=()
    local x
    for x in '*~/*' '*.png' '*.jpg' '*.jpeg' '*.tga' '*.psd' '*.exr' '*.tif' '*.tiff' '*.hdr' '*.fbx' '*.obj' \
        '*.asset' '*.mat' '*.unity' '*.prefab' '*.shadergraph' '*.shadersubgraph' '*.vfx' '*.md' '*.uxml' '*.uss' \
        '*.ttf' '*.otf' '*.anim' '*.controller' '*.wav' '*.mp4' '*.cubemap' '*.renderTexture' '*.pdf'; do
        excludes+=("--exclude=$x")
    done
    local start=$SECONDS
    if [[ -n "${JB_UNITY_TARBALL:-}" ]]; then
        log "Reading $JB_UNITY_TARBALL"
        tar -xJf "$JB_UNITY_TARBALL" -C "$UNITY_DIR.partial" --wildcards "${excludes[@]}" "${patterns[@]}"
    else
        log "Streaming $UNITY_URL (about 4.5 GB download, nothing large is stored; takes 5-10 minutes)"
        curl -fsSL --retry 5 --retry-all-errors "$UNITY_URL" \
            | tar -xJ -C "$UNITY_DIR.partial" --wildcards "${excludes[@]}" "${patterns[@]}"
    fi
    log "Extracted in $((SECONDS - start)) s ($(du -sh "$UNITY_DIR.partial" | cut -f1))"
    rm -rf "$UNITY_DIR"
    mv "$UNITY_DIR.partial" "$UNITY_DIR"
    touch "$UNITY_DIR/.complete"
}

# iOS Build Support module: the UnityEditor.iOS extension assemblies (editor code compiled for the iOS target
# references them) and the iOS player's UnityEngine assemblies (the player API surface, without editor-only API).
fetch_ios_support() {
    if [[ -f "$IOS_DIR/.complete" ]]; then
        return
    fi
    log "iOS Build Support $UNITY_VERSION not cached; extracting the needed parts into $IOS_DIR (about 335 MB download)"
    rm -rf "$IOS_DIR.partial"
    mkdir -p "$IOS_DIR.partial"
    local start=$SECONDS
    local src="$IOS_URL"
    if [[ -n "${JB_IOS_TARBALL:-}" ]]; then
        src="$JB_IOS_TARBALL"
        tar -xJf "$src" -C "$IOS_DIR.partial" --wildcards --no-wildcards-match-slash \
            './Editor/Data/PlaybackEngines/iOSSupport/*.dll' \
            './Editor/Data/PlaybackEngines/iOSSupport/Variations/il2cpp/Managed/*.dll'
    else
        curl -fsSL --retry 5 --retry-all-errors "$src" \
            | tar -xJ -C "$IOS_DIR.partial" --wildcards --no-wildcards-match-slash \
                './Editor/Data/PlaybackEngines/iOSSupport/*.dll' \
                './Editor/Data/PlaybackEngines/iOSSupport/Variations/il2cpp/Managed/*.dll'
    fi
    log "Extracted in $((SECONDS - start)) s ($(du -sh "$IOS_DIR.partial" | cut -f1))"
    rm -rf "$IOS_DIR"
    mv "$IOS_DIR.partial" "$IOS_DIR"
    touch "$IOS_DIR/.complete"
}

fetch_dxc() {
    if [[ -f "$DXC_DIR/.complete" ]]; then
        return
    fi
    log "DXC not cached; extracting it from the Vulkan SDK $VULKAN_SDK_VERSION into $DXC_DIR (about 370 MB download)"
    rm -rf "$DXC_DIR.partial"
    mkdir -p "$DXC_DIR.partial"
    local start=$SECONDS
    local members=("$VULKAN_SDK_VERSION/x86_64/bin/dxc*" "$VULKAN_SDK_VERSION/x86_64/lib/libdxcompiler.so*")
    if [[ -n "${JB_VULKAN_SDK_TARBALL:-}" ]]; then
        tar -xJf "$JB_VULKAN_SDK_TARBALL" -C "$DXC_DIR.partial" --wildcards "${members[@]}"
    else
        curl -fsSL --retry 5 --retry-all-errors "$VULKAN_SDK_URL" \
            | tar -xJ -C "$DXC_DIR.partial" --wildcards "${members[@]}"
    fi
    mv "$DXC_DIR.partial/$VULKAN_SDK_VERSION/x86_64/bin" "$DXC_DIR.partial/bin"
    mv "$DXC_DIR.partial/$VULKAN_SDK_VERSION/x86_64/lib" "$DXC_DIR.partial/lib"
    rm -rf "${DXC_DIR:?}.partial/$VULKAN_SDK_VERSION"
    log "Extracted in $((SECONDS - start)) s ($(du -sh "$DXC_DIR.partial" | cut -f1))"
    rm -rf "$DXC_DIR"
    mv "$DXC_DIR.partial" "$DXC_DIR"
    touch "$DXC_DIR/.complete"
}

main() {
    local fetch_only=0 shaders=1
    local configs=()
    local arg
    for arg in "$@"; do
        case "$arg" in
            --fetch-only) fetch_only=1 ;;
            --no-shaders) shaders=0 ;;
            --shaders) shaders=1 ;;
            -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
            *) configs+=("$arg") ;;
        esac
    done

    command -v python3 >/dev/null || { log "python3 is required"; exit 2; }
    command -v curl >/dev/null || { log "curl is required"; exit 2; }
    mkdir -p "$CACHE"
    fetch_unity
    fetch_ios_support
    if [[ $shaders -eq 1 || $fetch_only -eq 1 ]]; then
        fetch_dxc
    fi
    # Registry packages (exact versions from packages-lock.json) are fetched by the Python driver.
    python3 -I "$SCRIPT_DIR/compile_check.py" --fetch-only \
        --project "$PROJECT" --unity "$UNITY_DIR" --cache "$CACHE" --unity-version "$UNITY_VERSION"
    if [[ $fetch_only -eq 1 ]]; then
        exit 0
    fi

    local status=0
    python3 -I "$SCRIPT_DIR/compile_check.py" \
        --project "$PROJECT" --unity "$UNITY_DIR" --ios-support "$IOS_DIR" --cache "$CACHE" \
        --unity-version "$UNITY_VERSION" ${configs[@]+"${configs[@]}"} || status=$?
    if [[ $shaders -eq 1 ]]; then
        python3 -I "$SCRIPT_DIR/shader_check.py" --project "$PROJECT" --unity "$UNITY_DIR" --dxc "$DXC_DIR" \
            || status=1
    fi
    exit $status
}

main "$@"
