#!/bin/zsh
# Headless Unity on the owner's Mac.
#   tools/ci/unity.sh compile            open the project, compile, print compiler errors/warnings
#   tools/ci/unity.sh test [EditMode|PlayMode]   run tests, print a summary and any failures
#   tools/ci/unity.sh method <Class.Method> [args...]   run an editor method (-executeMethod)
# Logs and results go to $JB_OUT (default: /tmp/junglebooze-unity). Fails if the Unity editor has the project open.
set -u
ROOT=${0:A:h:h:h}
PROJECT="$ROOT/UnityProject"
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity}
OUT=${JB_OUT:-/tmp/junglebooze-unity}
mkdir -p "$OUT"
mode=${1:-compile}

summarize_log() {
  grep -E "error CS|warning CS|Scripts have compiler errors|Exception:|\[JungleBooze\]" "$1" | sort -u | head -80
}

case "$mode" in
  compile)
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -quit -logFile "$OUT/compile.log"
    code=$?
    summarize_log "$OUT/compile.log"
    echo "compile: unity exit $code (log $OUT/compile.log)"
    exit $code
    ;;
  test)
    platform=${2:-EditMode}
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform "$platform" \
      -testResults "$OUT/$platform.xml" -logFile "$OUT/$platform.log"
    code=$?
    if [[ -f "$OUT/$platform.xml" ]]; then
      python3 - "$OUT/$platform.xml" <<'EOF'
import sys, xml.etree.ElementTree as E
r = E.parse(sys.argv[1]).getroot()
print(f"{r.get('result')}: total {r.get('total')}, passed {r.get('passed')}, failed {r.get('failed')}, skipped {r.get('skipped')}")
for t in r.iter('test-case'):
    if t.get('result') not in ('Passed', 'Skipped'):
        msg = t.find('.//message')
        print(' FAIL', t.get('fullname'), '-', (msg.text or '').strip().splitlines()[0] if msg is not None and msg.text else '')
EOF
    else
      summarize_log "$OUT/$platform.log"
      echo "no results file (log $OUT/$platform.log)"
    fi
    exit $code
    ;;
  method)
    shift
    m=$1; shift
    "$UNITY" -batchmode -projectPath "$PROJECT" -executeMethod "$m" -quit -logFile "$OUT/method.log" "$@"
    code=$?
    summarize_log "$OUT/method.log"
    echo "method $m: unity exit $code (log $OUT/method.log)"
    exit $code
    ;;
  *)
    echo "usage: $0 compile | test [EditMode|PlayMode] | method <Class.Method> [args]" >&2
    exit 2
    ;;
esac
