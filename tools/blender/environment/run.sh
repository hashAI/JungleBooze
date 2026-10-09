#!/bin/bash
# Run a Blender environment script headless: tools/blender/environment/run.sh <script.py> [args...]
export PYTHONPATH="$HOME/Library/Application Support/Blender/4.2/scripts/modules"
s="$1"; shift
exec "$HOME/Applications/Blender42.app/Contents/MacOS/Blender" -b --factory-startup --python-use-system-env \
  -P "$(dirname "$0")/$s" -- "$@"
