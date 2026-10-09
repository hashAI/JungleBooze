#!/bin/bash
# iPhone performance benchmark: builds the painterly hero basin + Expedition bench player, installs it on the
# connected iPhone and starts it. Works with a free Apple ID (Xcode "Personal Team"). Run on the owner's Mac:
#
#     tools/build/device_bench.sh                 build (development), sign, install and launch on the iPhone
#     tools/build/device_bench.sh --release       same with a release player (closest to shipping numbers)
#     tools/build/device_bench.sh --export-only   only export the Xcode project, then press Run in Xcode yourself
#     tools/build/device_bench.sh --pull          copy the benchmark logs from the iPhone to UnityProject/Builds/bench-logs
#     tools/build/device_bench.sh --clean         delete the bench build folders (frees several GB)
#   options: --team TEAMID  --device NAME|UDID  --bundle-id ID
#
# What runs on the phone (about 15 minutes, fully automatic; screen stays on):
#   hero basin landscape 60 s, portrait 60 s, Expedition bot landscape 90 s, portrait 60 s, then a 10-minute soak on
#   the hero basin landscape view. The overlay shows FPS, frame time p50/p95/p99, CPU/GPU ms, thermal state, memory.
#   Logs: the app's Documents/bench folder (CSV per second + summary): use --pull, or Finder > iPhone > Files, or
#   Xcode > Window > Devices and Simulators > Aurelia Bench > Download Container.
# Guide: docs/perf/2026-10-iphone12-readiness.md ("Owner steps"). Works with the Bash 3.2 that ships with macOS.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/UnityProject"
SETTINGS_DIR="$HOME/.junglebooze"
BENCH_ENV="$SETTINGS_DIR/bench.env"
EXPORT_DIR="$PROJECT/Builds/iOS-Bench"
DERIVED_DIR="$PROJECT/Builds/iOS-Bench-DD"
LOG_DIR="$PROJECT/Builds/logs"
HERO_SCENE="$PROJECT/Assets/_Game/Scenes/HeroBasin_Painterly.unity"
MIN_FREE_GB=6

MODE="run"
BUILD_KIND="development"
TEAM="${JB_TEAM_ID:-}"
DEVICE="${JB_DEVICE:-}"
BUNDLE_ID="${JB_BENCH_BUNDLE_ID:-}"

if [ -t 1 ]; then
  RED=$'\033[31m'; GREEN=$'\033[32m'; YELLOW=$'\033[33m'; BOLD=$'\033[1m'; RESET=$'\033[0m'
else
  RED=""; GREEN=""; YELLOW=""; BOLD=""; RESET=""
fi
ok()   { echo "${GREEN}  OK${RESET}  $1"; }
warn() { echo "${YELLOW}  !!${RESET}  $1"; }
step() { echo; echo "${BOLD}$1${RESET}"; }
fail() {
  echo; echo "${RED}${BOLD}STOPPED:${RESET} $1"
  if [ -n "${2:-}" ]; then echo "${BOLD}How to fix:${RESET} $2"; fi
  echo "Guide: docs/perf/2026-10-iphone12-readiness.md (Owner steps)"
  exit 1
}
usage() { sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; }

while [ $# -gt 0 ]; do
  case "$1" in
    --release) BUILD_KIND="release" ;;
    --export-only) MODE="export" ;;
    --pull) MODE="pull" ;;
    --clean) MODE="clean" ;;
    --team) shift; TEAM="${1:-}" ;;
    --device) shift; DEVICE="${1:-}" ;;
    --bundle-id) shift; BUNDLE_ID="${1:-}" ;;
    -h|--help) usage; exit 0 ;;
    *) usage; fail "Unknown option '$1'." "Run tools/build/device_bench.sh --help." ;;
  esac
  shift
done

# ---------- bundle id: one stable id per Mac (free Apple IDs may register only a few new ids per week) ----------
mkdir -p "$SETTINGS_DIR"
if [ -z "$BUNDLE_ID" ] && [ -f "$BENCH_ENV" ]; then
  BUNDLE_ID="$(sed -n 's/^JB_BENCH_BUNDLE_ID=//p' "$BENCH_ENV" | head -n 1)"
fi
if [ -z "$BUNDLE_ID" ]; then
  user_part="$(id -un | tr 'A-Z' 'a-z' | tr -cd 'a-z0-9')"
  host_part="$(hostname | shasum | cut -c1-6)"
  BUNDLE_ID="com.${user_part:-owner}.aureliabench.b${host_part}"
  echo "JB_BENCH_BUNDLE_ID=$BUNDLE_ID" > "$BENCH_ENV"
fi

# ---------- device lookup (first connected, paired iPhone unless --device) ----------
find_device() {
  local json
  json="$(mktemp -t jbdevices)"
  if ! xcrun devicectl list devices --json-output "$json" >/dev/null 2>&1; then
    rm -f "$json"; return 1
  fi
  python3 -I - "$json" "$DEVICE" <<'EOF'
import json, sys
data = json.load(open(sys.argv[1]))
want = sys.argv[2].lower()
for d in data.get("result", {}).get("devices", []):
    props = d.get("deviceProperties", {})
    hw = d.get("hardwareProperties", {})
    conn = d.get("connectionProperties", {})
    name = props.get("name", "")
    udid = hw.get("udid", "")
    if hw.get("platform") != "iOS" or hw.get("reality") == "virtual":
        continue
    if want and want not in (name.lower(), udid.lower(), d.get("identifier", "").lower()):
        continue
    if conn.get("tunnelState") in ("connected", "disconnected") and conn.get("pairingState") == "paired":
        print(udid + "\t" + name + "\t" + hw.get("productType", "?") + "\t" + conn.get("tunnelState", ""))
        break
EOF
  rm -f "$json"
}

# ---------- --clean ----------
if [ "$MODE" = "clean" ]; then
  rm -rf "$EXPORT_DIR" "$DERIVED_DIR"
  ok "Deleted $EXPORT_DIR and $DERIVED_DIR"
  exit 0
fi

# ---------- --pull ----------
if [ "$MODE" = "pull" ]; then
  step "Copying benchmark logs from the iPhone"
  info="$(find_device || true)"
  [ -n "$info" ] || fail "No paired iPhone found." "Plug the iPhone in, unlock it, and run this again."
  udid="$(echo "$info" | cut -f1)"
  dest="$PROJECT/Builds/bench-logs/$(date +%Y%m%d-%H%M%S)"
  mkdir -p "$dest"
  if xcrun devicectl device copy from --device "$udid" --domain-type appDataContainer --domain-identifier "$BUNDLE_ID" \
       --source Documents/bench --destination "$dest" >/dev/null 2>&1; then
    ok "Logs copied to $dest"
    ls -1 "$dest" | sed 's/^/      /'
    exit 0
  fi
  fail "Could not copy the logs with devicectl." \
       "Use Finder: click the iPhone in the sidebar > Files > Aurelia Bench > drag the 'bench' folder to the Desktop. Or Xcode > Window > Devices and Simulators > Aurelia Bench > (gear) > Download Container."
fi

# ---------- checks ----------
step "1/5 Tools"
[ "$(uname -s)" = "Darwin" ] || fail "This script must run on a Mac."
xcodebuild -version >/dev/null 2>&1 || fail "Xcode is not ready." "Install Xcode from the App Store, open it once, then: sudo xcode-select -s /Applications/Xcode.app"
ok "$(xcodebuild -version | head -n 1), iOS SDK $(xcrun --sdk iphoneos --show-sdk-version 2>/dev/null || echo '?')"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$PROJECT/ProjectSettings/ProjectVersion.txt" | tr -d '[:space:]')"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
[ -x "$UNITY_PATH" ] || fail "Unity $UNITY_VERSION not found at $UNITY_PATH" "Install it with iOS Build Support in Unity Hub."
[ -d "$(dirname "$UNITY_PATH")/../../../PlaybackEngines/iOSSupport" ] || fail "Unity's iOS Build Support is not installed." \
  "Unity Hub > Installs > $UNITY_VERSION > gear > Add modules > iOS Build Support."
if ps -axo command | grep -i "[U]nity.app/Contents/MacOS/Unity" | grep -iF -- "-projectpath $PROJECT" >/dev/null 2>&1; then
  fail "Unity has this project open." "Quit Unity (or wait for the running batch job) and run this again."
fi
free_gb="$(df -g "$PROJECT" | awk 'NR==2 {print $4}')"
[ "${free_gb:-0}" -ge "$MIN_FREE_GB" ] || fail "Only ${free_gb} GB free disk space; the build needs about $MIN_FREE_GB GB." \
  "Free some space (tools/build/device_bench.sh --clean removes old bench builds)."
ok "Unity $UNITY_VERSION with iOS support, ${free_gb} GB free"

mkdir -p "$LOG_DIR"
stamp="$(date +%Y%m%d-%H%M%S)"

step "2/5 Hero basin scene"
if [ ! -f "$HERO_SCENE" ]; then
  echo "     Building the painterly hero basin scene (first time only, a few minutes)..."
  "$UNITY_PATH" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod JungleBooze.Editor.HeroBasin.HeroBasinBatch.BuildScene -jbHeroStyle painterly \
    -logFile "$LOG_DIR/device_bench-hero-$stamp.log" || fail "Building the hero scene failed. Log: $LOG_DIR/device_bench-hero-$stamp.log"
fi
ok "HeroBasin_Painterly.unity present"

step "3/5 Unity export (iOS, $BUILD_KIND, bundle id $BUNDLE_ID)"
if [ -z "$TEAM" ]; then
  # Team id of the first Apple Development certificate (created when you sign in to Xcode and build once).
  TEAM="$(security find-certificate -a -c "Apple Development" -p 2>/dev/null | openssl x509 -noout -subject 2>/dev/null \
          | sed -n 's/.*OU *= *\([A-Z0-9]\{10\}\).*/\1/p' | head -n 1 || true)"
fi
unity_args=(-jbOutput "$EXPORT_DIR" -jbBundleId "$BUNDLE_ID")
[ -n "$TEAM" ] && unity_args+=(-jbTeamId "$TEAM")
[ "$BUILD_KIND" = "release" ] && unity_args+=(-jbRelease)
echo "     This takes 5 to 20 minutes (the first iOS export re-imports textures)."
caffeinate -dims "$UNITY_PATH" -batchmode -quit -projectPath "$PROJECT" \
  -executeMethod JungleBooze.Editor.Build.DeviceBenchBuild.BuildIos "${unity_args[@]}" \
  -logFile "$LOG_DIR/device_bench-$stamp.log" || fail "The Unity export failed. Log: $LOG_DIR/device_bench-$stamp.log" \
  "Search the log for 'error'. Send it to the team if it is not clear."
[ -d "$EXPORT_DIR/Unity-iPhone.xcodeproj" ] || fail "No Xcode project in $EXPORT_DIR. Log: $LOG_DIR/device_bench-$stamp.log"
ok "Xcode project: $EXPORT_DIR/Unity-iPhone.xcodeproj"

print_manual() {
  echo
  echo "${BOLD}Finish in Xcode:${RESET}"
  echo "  1. open \"$EXPORT_DIR/Unity-iPhone.xcodeproj\""
  echo "  2. Click 'Unity-iPhone' (left, top) > target Unity-iPhone > Signing & Capabilities > tick 'Automatically manage"
  echo "     signing' > Team: your name (Personal Team)."
  echo "  3. Choose your iPhone in the device menu at the top, then press Run (the triangle)."
}

if [ "$MODE" = "export" ]; then
  print_manual
  exit 0
fi

step "4/5 iPhone and signing"
info="$(find_device || true)"
if [ -z "$info" ]; then
  warn "No paired iPhone found (plug it in, unlock it, tap Trust)."
  print_manual
  exit 0
fi
udid="$(echo "$info" | cut -f1)"
ok "iPhone: $(echo "$info" | cut -f2) ($(echo "$info" | cut -f3))"
if [ -z "$TEAM" ]; then
  warn "No signing team found on this Mac (sign in to Xcode first: Xcode > Settings > Accounts > + > Apple ID)."
  print_manual
  exit 0
fi
ok "Signing team $TEAM"

step "5/5 Build, install and launch"
config="Release"
build_log="$LOG_DIR/device_bench-xcode-$stamp.log"
if ! caffeinate -dims xcodebuild -project "$EXPORT_DIR/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration "$config" \
      -destination "id=$udid" -derivedDataPath "$DERIVED_DIR" -allowProvisioningUpdates \
      DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic build > "$build_log" 2>&1; then
  grep -E "error:|Signing|provision" "$build_log" | head -n 8 || true
  print_manual
  fail "Xcode could not build or sign the app. Log: $build_log" \
       "Most often: sign in to Xcode with your Apple ID (Xcode > Settings > Accounts), then run again or finish in Xcode as shown above."
fi
app="$(find "$DERIVED_DIR/Build/Products/$config-iphoneos" -maxdepth 1 -name "*.app" | head -n 1)"
[ -n "$app" ] || fail "Built app not found under $DERIVED_DIR."
xcrun devicectl device install app --device "$udid" "$app" >/dev/null || fail "Installing on the iPhone failed." \
  "Unlock the iPhone and run again. On iOS 16+ turn on Settings > Privacy & Security > Developer Mode."
if ! xcrun devicectl device process launch --device "$udid" "$BUNDLE_ID" >/dev/null 2>&1; then
  warn "Installed, but iOS did not let it start yet (normal the first time with a free Apple ID)."
  echo "     On the iPhone: Settings > General > VPN & Device Management > your Apple ID > Trust."
  echo "     Then tap the 'Aurelia Bench' icon."
else
  ok "Aurelia Bench is running on the iPhone."
fi

echo
echo "${GREEN}${BOLD}Benchmark started.${RESET} It runs about 15 minutes by itself (screen stays on)."
echo "  - Unplug the cable after it starts and leave the phone on a table, case off, brightness about half."
echo "  - When it shows 'BENCH DONE', plug in and run:  tools/build/device_bench.sh --pull"
echo "  - Send the folder it prints to the team."
