#!/usr/bin/env bash
#
# Builds the C# solution and runs the headless smoke test.
# Exits 0 only if every check passed.
#
# Override the engine with GODOT=/path/to/godot ./run-smoke-test.sh
#
set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Locate a Godot .NET editor binary. Order matters: an explicit GODOT wins,
# then anything on PATH, then the usual macOS bundle names, then the Flatpak
# that SteamOS installs.
find_godot() {
  if [[ -n "${GODOT:-}" ]]; then
    GODOT_CMD=("$GODOT")
    return 0
  fi

  local candidate
  for candidate in godot4 godot; do
    if command -v "$candidate" >/dev/null 2>&1; then
      GODOT_CMD=("$candidate")
      return 0
    fi
  done

  # The cask has shipped under both of these names.
  for candidate in \
    "/Applications/Godot_mono.app/Contents/MacOS/Godot" \
    "/Applications/Godot Mono.app/Contents/MacOS/Godot"
  do
    if [[ -x "$candidate" ]]; then
      GODOT_CMD=("$candidate")
      return 0
    fi
  done

  if command -v flatpak >/dev/null 2>&1 \
     && flatpak info org.godotengine.GodotSharp >/dev/null 2>&1; then
    GODOT_CMD=(flatpak run org.godotengine.GodotSharp)
    return 0
  fi

  return 1
}

if ! find_godot; then
  echo "error: no Godot .NET editor found." >&2
  echo "Set GODOT=/path/to/godot, or install it — see AGENTS.md." >&2
  exit 127
fi

echo "==> using: ${GODOT_CMD[*]}"

echo "==> building solution"
if ! "${GODOT_CMD[@]}" --headless --path "$PROJECT_DIR" --build-solutions --quit >/dev/null 2>&1; then
  echo "error: the solution failed to build." >&2
  "${GODOT_CMD[@]}" --headless --path "$PROJECT_DIR" --build-solutions --quit 2>&1 | tail -30 >&2
  exit 1
fi

echo "==> running smoke test"
output="$(mktemp)"
trap 'rm -f "$output"' EXIT

# NOTE: --build-solutions together with a scene path hangs; keep them separate.
"${GODOT_CMD[@]}" --headless --path "$PROJECT_DIR" res://scenes/SmokeTest.tscn 2>&1 | tee "$output"
status=${PIPESTATUS[0]}

# A broken assembly can leave Godot exiting 0 without running anything, so
# require the summary line before trusting the exit code.
if ! grep -q '^==== .* passed, .* failed ====$' "$output"; then
  echo "error: the smoke test did not run to completion." >&2
  exit 1
fi

exit "$status"
