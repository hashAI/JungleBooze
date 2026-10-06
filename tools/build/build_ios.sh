#!/bin/bash
# Builds the game for iPhone and uploads it to TestFlight. Run on the owner's Mac from anywhere:
#
#     tools/build/build_ios.sh               check everything, then build and upload to TestFlight
#     tools/build/build_ios.sh --check       only check that everything is set up (builds nothing)
#     tools/build/build_ios.sh --init        create your private settings file ~/.junglebooze/release.env
#     tools/build/build_ios.sh --certs-setup one-time: create the signing certificate and profile
#     tools/build/build_ios.sh --certs       download the existing certificate and profile (read-only)
#     tools/build/build_ios.sh --development build a development player instead of a release player
#
# Step-by-step setup guide: docs/RELEASE.md. Works with the Bash 3.2 that ships with macOS.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SETTINGS_DIR="$HOME/.junglebooze"
ENV_FILE="${JB_ENV_FILE:-$SETTINGS_DIR/release.env}"
GUIDE="docs/RELEASE.md"

MODE="beta"
LANE_ARGS=()

# ---------- output helpers ----------
if [ -t 1 ]; then
  RED=$'\033[31m'; GREEN=$'\033[32m'; YELLOW=$'\033[33m'; BOLD=$'\033[1m'; RESET=$'\033[0m'
else
  RED=""; GREEN=""; YELLOW=""; BOLD=""; RESET=""
fi

ok()   { echo "${GREEN}  OK${RESET}  $1"; }
warn() { echo "${YELLOW}  !!${RESET}  $1"; }
step() { echo; echo "${BOLD}$1${RESET}"; }

# fail "what is wrong" "how to fix it"
fail() {
  echo
  echo "${RED}${BOLD}STOPPED:${RESET} $1"
  if [ -n "${2:-}" ]; then
    echo "${BOLD}How to fix:${RESET} $2"
  fi
  echo "Setup guide: $GUIDE"
  exit 1
}

usage() {
  sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'
}

# ---------- arguments ----------
while [ $# -gt 0 ]; do
  case "$1" in
    --check) MODE="check" ;;
    --init) MODE="init" ;;
    --certs-setup) MODE="certs-setup" ;;
    --certs) MODE="certs" ;;
    --development) LANE_ARGS+=("build_type:development") ;;
    -h|--help) usage; exit 0 ;;
    *) usage; fail "Unknown option '$1'." "Run tools/build/build_ios.sh --help to see the options." ;;
  esac
  shift
done

cd "$REPO_ROOT"

# ---------- --init: create the private settings file ----------
if [ "$MODE" = "init" ]; then
  mkdir -p "$SETTINGS_DIR"
  chmod 700 "$SETTINGS_DIR"
  if [ -f "$ENV_FILE" ]; then
    ok "Settings file already exists: $ENV_FILE (left unchanged)"
  else
    cp "$SCRIPT_DIR/release.env.example" "$ENV_FILE"
    chmod 600 "$ENV_FILE"
    ok "Created $ENV_FILE (only you can read it)."
  fi
  echo "Next: open it with:  open -e \"$ENV_FILE\"   and fill in the values (guide step 8)."
  exit 0
fi

step "1/7 Computer"
[ "$(uname -s)" = "Darwin" ] || fail "This script must run on a Mac." "Run it on the Mac that has Xcode and Unity."
ok "macOS $(sw_vers -productVersion)"

# fastlane needs a UTF-8 locale.
export LC_ALL="${LC_ALL:-en_US.UTF-8}"
export LANG="${LANG:-en_US.UTF-8}"

# ---------- Xcode ----------
step "2/7 Xcode"
# shellcheck disable=SC1091
. "$SCRIPT_DIR/apple-minimums.env"
DEV_DIR="$(xcode-select -p 2>/dev/null || true)"
case "$DEV_DIR" in
  *CommandLineTools*|"")
    fail "Full Xcode is not selected (found: '${DEV_DIR:-nothing}')." \
         "Install Xcode from the Mac App Store, open it once, then run: sudo xcode-select -s /Applications/Xcode.app" ;;
esac
XCODE_VERSION_TEXT="$(xcodebuild -version 2>&1 | head -n 1 || true)"
XCODE_MAJOR="$(echo "$XCODE_VERSION_TEXT" | sed -n 's/^Xcode \([0-9][0-9]*\).*/\1/p')"
[ -n "$XCODE_MAJOR" ] || fail "Xcode did not start: $XCODE_VERSION_TEXT" \
  "Open Xcode once and accept the license, or run: sudo xcodebuild -license accept"
if [ "$XCODE_MAJOR" -lt "$MIN_XCODE_MAJOR" ]; then
  fail "$XCODE_VERSION_TEXT is too old. Apple requires Xcode $MIN_XCODE_MAJOR or newer for App Store uploads." \
       "Update Xcode in the Mac App Store (App Store > Updates)."
fi
SDK_VERSION="$(xcrun --sdk iphoneos --show-sdk-version 2>/dev/null || true)"
[ -n "$SDK_VERSION" ] || fail "The iOS SDK is missing from Xcode." \
  "Open Xcode > Settings > Components and install the iOS platform."
if [ "${SDK_VERSION%%.*}" -lt "$MIN_IOS_SDK_MAJOR" ]; then
  fail "iOS SDK $SDK_VERSION is too old. Apple requires iOS SDK $MIN_IOS_SDK_MAJOR or newer." "Update Xcode."
fi
ok "$XCODE_VERSION_TEXT, iOS SDK $SDK_VERSION (Apple minimum: Xcode $MIN_XCODE_MAJOR, checked $CHECKED_ON)"

# ---------- settings file ----------
step "3/7 Your private settings"
[ -f "$ENV_FILE" ] || fail "Settings file not found: $ENV_FILE" \
  "Run: tools/build/build_ios.sh --init   then fill in the file (guide step 8)."
PERMS="$(stat -f '%Lp' "$ENV_FILE")"
if [ "$PERMS" != "600" ] && [ "$PERMS" != "400" ]; then
  chmod 600 "$ENV_FILE"
  warn "Made $ENV_FILE readable only by you (it was $PERMS)."
fi
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

MISSING=""
for name in JB_BUNDLE_ID APPLE_TEAM_ID ASC_KEY_ID ASC_ISSUER_ID ASC_KEY_PATH MATCH_GIT_URL MATCH_PASSWORD; do
  value="${!name:-}"
  [ -n "$value" ] || MISSING="$MISSING $name"
done
[ -z "$MISSING" ] || fail "These settings are empty in $ENV_FILE:$MISSING" "Fill them in (guide step 8)."

echo "$APPLE_TEAM_ID" | grep -Eq '^[A-Z0-9]{10}$' || fail "APPLE_TEAM_ID '$APPLE_TEAM_ID' should be 10 capital letters/digits." \
  "Copy it from developer.apple.com > Account > Membership details."
echo "$JB_BUNDLE_ID" | grep -Eq '^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$' || fail "JB_BUNDLE_ID '$JB_BUNDLE_ID' is not a valid bundle identifier." \
  "Use the exact identifier you registered (guide step 3), for example com.yourname.yourgame."
[ -f "$ASC_KEY_PATH" ] || fail "App Store Connect key file not found: $ASC_KEY_PATH" \
  "Move the downloaded AuthKey_....p8 into $SETTINGS_DIR and put its full path in ASC_KEY_PATH (guide step 5)."
case "$(cd "$(dirname "$ASC_KEY_PATH")" && pwd)/" in
  "$REPO_ROOT"/*) fail "The App Store Connect key is inside the project folder, where it could be committed." \
                       "Move it to $SETTINGS_DIR and update ASC_KEY_PATH." ;;
esac
ok "Settings loaded from $ENV_FILE (bundle id $JB_BUNDLE_ID, team $APPLE_TEAM_ID)"

# ---------- Unity ----------
step "4/7 Unity"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: *//p' UnityProject/ProjectSettings/ProjectVersion.txt | tr -d '[:space:]')"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
export UNITY_PATH
[ -x "$UNITY_PATH" ] || fail "Unity $UNITY_VERSION was not found at $UNITY_PATH" \
  "In Unity Hub > Installs > Install Editor, install exactly $UNITY_VERSION with 'iOS Build Support' (guide step 2)."
UNITY_EDITOR_DIR="$(cd "$(dirname "$UNITY_PATH")/../../.." && pwd)"
[ -d "$UNITY_EDITOR_DIR/PlaybackEngines/iOSSupport" ] || fail "Unity's iOS Build Support module is not installed." \
  "Unity Hub > Installs > $UNITY_VERSION > gear icon > Add modules > tick 'iOS Build Support'."
if pgrep -f "$UNITY_EDITOR_DIR" >/dev/null 2>&1; then
  fail "Unity is open. Batch builds cannot run while the project is open in the editor." "Quit Unity and run this again."
fi
ok "Unity $UNITY_VERSION with iOS Build Support"

# ---------- Ruby and fastlane ----------
step "5/7 Ruby and fastlane"
# macOS's built-in Ruby is too old; prefer Homebrew's Ruby when it is installed.
for brew_ruby in /opt/homebrew/opt/ruby/bin /usr/local/opt/ruby/bin; do
  if [ -x "$brew_ruby/ruby" ]; then
    PATH="$brew_ruby:$PATH"
    break
  fi
done
command -v ruby >/dev/null 2>&1 || fail "Ruby is not installed." "Run: brew install ruby (guide step 1)."
ruby -e 'exit(Gem::Version.new(RUBY_VERSION) >= Gem::Version.new("3.2") ? 0 : 1)' \
  || fail "Ruby $(ruby -e 'print RUBY_VERSION') is too old (3.2 or newer needed)." "Run: brew install ruby (guide step 1)."
GEM_BIN="$(ruby -e 'print Gem.bindir')"
PATH="$GEM_BIN:$PATH"
export PATH
command -v bundle >/dev/null 2>&1 || gem install bundler --no-document
# Gems go to your settings folder so the project folder stays unchanged.
export BUNDLE_PATH="$SETTINGS_DIR/gems"
export BUNDLE_GEMFILE="$REPO_ROOT/Gemfile"
export FASTLANE_SKIP_UPDATE_CHECK=1
export FASTLANE_HIDE_CHANGELOG=1
if ! bundle check >/dev/null 2>&1; then
  echo "     Installing fastlane (first run only, a few minutes)..."
  bundle install --quiet || fail "Installing fastlane failed (see the messages above)." \
    "Check the internet connection and run again. If it repeats, send the messages to the team."
fi
ok "Ruby $(ruby -e 'print RUBY_VERSION'), $(bundle exec fastlane --version 2>/dev/null | grep -Eo 'fastlane [0-9.]+' | head -n 1)"

# ---------- git ----------
step "6/7 Project files"
command -v git-lfs >/dev/null 2>&1 || fail "Git LFS is not installed (the project stores art and audio with it)." \
  "Run: brew install git-lfs && git lfs install"
git lfs pull >/dev/null 2>&1 || fail "Could not download the large project files (git lfs pull)." \
  "Check the internet connection and that you are signed in to GitHub (guide step 6)."
if [ -n "$(git status --porcelain)" ]; then
  git status --short
  fail "The project folder has changes that are not on GitHub (listed above). Builds must match GitHub exactly." \
       "Do not delete anything. Send this list to the team; they will commit or clean it up."
fi
ok "Project folder is clean, branch $(git rev-parse --abbrev-ref HEAD), commit $(git rev-parse --short HEAD)"

# ---------- run ----------
LOG_DIR="$REPO_ROOT/UnityProject/Builds/logs"
mkdir -p "$LOG_DIR"
RUN_LOG="$LOG_DIR/build_ios-$(date +%Y%m%d-%H%M%S).log"

run_lane() {
  # caffeinate keeps the Mac awake for the whole run; output also goes to a log file to send to the team.
  caffeinate -dims bundle exec fastlane ios "$@" 2>&1 | tee "$RUN_LOG"
}

step "7/7 Running ($MODE)"
case "$MODE" in
  check)
    run_lane preflight || fail "A check failed (see the red message above). Log: $RUN_LOG" "Follow the message, or send the log to the team."
    echo; echo "${GREEN}${BOLD}All checks passed.${RESET} Run tools/build/build_ios.sh to build and upload." ;;
  certs-setup)
    run_lane certs readonly:false || fail "Creating the signing certificate failed. Log: $RUN_LOG" "Send the log to the team."
    echo; echo "${GREEN}${BOLD}Signing certificate and profile are ready.${RESET}" ;;
  certs)
    run_lane certs || fail "Downloading the signing certificate failed. Log: $RUN_LOG" "Send the log to the team." ;;
  beta)
    echo "     This takes 20 to 60 minutes. The Mac will stay awake; you can leave it."
    run_lane beta ${LANE_ARGS[@]+"${LANE_ARGS[@]}"} || fail "The build did not finish (see the red message above). Log: $RUN_LOG" \
      "Follow the message, or send the log file to the team."
    echo; echo "${GREEN}${BOLD}Done.${RESET} The build is on TestFlight. Open the TestFlight app on your iPhone in a few minutes." ;;
esac
