#!/usr/bin/env bash
#
# Sets up a development environment for this project on macOS or Linux
# (including SteamOS), then imports the Godot project so it is ready to open.
#
#   ./bootstrap.sh            show a plan, prompt before installing anything
#   ./bootstrap.sh --yes      install without prompting
#   ./bootstrap.sh --check    report what is missing, install nothing
#
# Safe to re-run: everything it does is idempotent.
#
set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSUME_YES=0
CHECK_ONLY=0

for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=1 ;;
    --check|-n) CHECK_ONLY=1 ;;
    --help|-h) sed -n '2,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown option: $arg (try --help)" >&2; exit 2 ;;
  esac
done

info()  { printf '\033[1m==>\033[0m %s\n' "$*"; }
warn()  { printf '\033[33mwarning:\033[0m %s\n' "$*" >&2; }
fail()  { printf '\033[31merror:\033[0m %s\n' "$*" >&2; exit 1; }

confirm() {
  [[ $ASSUME_YES -eq 1 ]] && return 0
  [[ ! -t 0 ]] && fail "not a terminal; re-run with --yes to install non-interactively"
  read -r -p "$1 [y/N] " reply
  [[ "$reply" =~ ^[Yy] ]]
}

# ---------------------------------------------------------------- platform ---

OS="$(uname -s)"
DISTRO_ID=""
IS_STEAMOS=0

if [[ -r /etc/os-release ]]; then
  # shellcheck disable=SC1091
  DISTRO_ID="$(. /etc/os-release && echo "${ID:-}")"
  [[ "$DISTRO_ID" == "steamos" ]] && IS_STEAMOS=1
fi
# Fall back to a Deck-specific tool in case ID is not set as expected.
[[ -x /usr/bin/steamos-readonly ]] && IS_STEAMOS=1

case "$OS" in
  Darwin) PLATFORM="macos" ;;
  Linux)  PLATFORM=$([[ $IS_STEAMOS -eq 1 ]] && echo "steamos" || echo "linux") ;;
  *) fail "unsupported platform: $OS (this project targets macOS and Linux/SteamOS)" ;;
esac

info "platform: $PLATFORM"

# ------------------------------------------------------------------- godot ---

GODOT_CMD=()

detect_godot() {
  GODOT_CMD=()

  if [[ -n "${GODOT:-}" && -x "${GODOT}" ]]; then
    GODOT_CMD=("$GODOT"); return 0
  fi

  local candidate
  for candidate in godot4 godot; do
    command -v "$candidate" >/dev/null 2>&1 && { GODOT_CMD=("$candidate"); return 0; }
  done

  # The Homebrew cask has shipped under both of these bundle names.
  for candidate in \
    "/Applications/Godot_mono.app/Contents/MacOS/Godot" \
    "/Applications/Godot Mono.app/Contents/MacOS/Godot"
  do
    [[ -x "$candidate" ]] && { GODOT_CMD=("$candidate"); return 0; }
  done

  if command -v flatpak >/dev/null 2>&1 \
     && flatpak info org.godotengine.GodotSharp >/dev/null 2>&1; then
    GODOT_CMD=(flatpak run org.godotengine.GodotSharp); return 0
  fi

  return 1
}

# Godot must be the .NET/Mono build; the plain build cannot load any script here.
godot_has_dotnet() {
  local version
  version="$("${GODOT_CMD[@]}" --version 2>/dev/null | tail -1)"
  [[ "$version" == *mono* ]]
}

install_godot() {
  case "$PLATFORM" in
    macos)
      command -v brew >/dev/null 2>&1 \
        || fail "Homebrew not found. Install it from https://brew.sh, then re-run."
      confirm "Install Godot (.NET) via 'brew install --cask godot-mono'?" \
        || fail "Godot is required."
      brew install --cask godot-mono || fail "brew install failed"
      ;;
    steamos|linux)
      command -v flatpak >/dev/null 2>&1 \
        || fail "flatpak not found. Install flatpak, then re-run."
      if [[ $IS_STEAMOS -eq 1 ]]; then
        info "SteamOS has a read-only root filesystem; using Flatpak (pacman changes"
        info "would be wiped by the next system update)."
      fi
      confirm "Install Godot (.NET) via Flatpak (org.godotengine.GodotSharp)?" \
        || fail "Godot is required."
      flatpak install --user -y flathub org.godotengine.GodotSharp \
        || fail "flatpak install failed"
      ;;
  esac
}

# ------------------------------------------------------------------ dotnet ---

# The GodotSharp Flatpak bundles its own .NET SDK, so a host SDK is only needed
# when Godot is not running inside that sandbox.
needs_host_dotnet() {
  [[ "${GODOT_CMD[0]:-}" != "flatpak" ]]
}

dotnet_ok() {
  command -v dotnet >/dev/null 2>&1 || return 1
  local major
  major="$(dotnet --version 2>/dev/null | cut -d. -f1)"
  [[ -n "$major" && "$major" -ge 8 ]]
}

install_dotnet() {
  case "$PLATFORM" in
    macos)
      command -v brew >/dev/null 2>&1 \
        || fail "Homebrew not found. Install it from https://brew.sh, then re-run."
      confirm "Install the .NET SDK via 'brew install --cask dotnet-sdk'?" \
        || fail ".NET SDK 8+ is required."
      brew install --cask dotnet-sdk || fail "brew install failed"
      if [[ "$(uname -m)" == "arm64" ]]; then
        info "Apple Silicon: confirm the SDK is arm64 with 'dotnet --info | grep -i architecture'."
        info "An x64 SDK under Rosetta with an arm64 Godot fails at runtime, not at build time."
      fi
      ;;
    steamos)
      fail "On SteamOS, use the GodotSharp Flatpak (it bundles .NET) rather than
       installing an SDK onto the read-only root filesystem."
      ;;
    linux)
      if command -v apt-get >/dev/null 2>&1; then
        confirm "Install the .NET SDK via 'apt-get install dotnet-sdk-8.0' (needs sudo)?" \
          || fail ".NET SDK 8+ is required."
        sudo apt-get update -qq
        sudo apt-get install -y dotnet-sdk-8.0 || fail "apt-get install failed"
      else
        fail "Install .NET SDK 8 or newer using your distribution's package manager, then re-run.
       Note that Microsoft's dot.net installer endpoints are blocked on some
       networks; a distro package is usually the more reliable route."
      fi
      ;;
  esac
}

# ------------------------------------------------------------------- checks ---

MISSING=()

if detect_godot; then
  if godot_has_dotnet; then
    info "found Godot: ${GODOT_CMD[*]} ($("${GODOT_CMD[@]}" --version 2>/dev/null | tail -1))"
  else
    warn "found Godot at '${GODOT_CMD[*]}', but it is not the .NET/Mono build."
    warn "This project is entirely C#; the plain build cannot load any of it."
    MISSING+=("godot")
  fi
else
  info "Godot (.NET build): not found"
  MISSING+=("godot")
fi

if needs_host_dotnet; then
  if dotnet_ok; then
    info "found .NET SDK: $(dotnet --version)"
  else
    info ".NET SDK 8+: not found"
    MISSING+=("dotnet")
  fi
else
  info ".NET SDK: provided by the GodotSharp Flatpak"
fi

if [[ $CHECK_ONLY -eq 1 ]]; then
  if [[ ${#MISSING[@]} -eq 0 ]]; then
    info "everything required is present"
    exit 0
  fi
  warn "missing: ${MISSING[*]}"
  exit 1
fi

# ----------------------------------------------------------------- install ---

for item in "${MISSING[@]:-}"; do
  case "$item" in
    godot)  install_godot ;;
    dotnet) install_dotnet ;;
  esac
done

if [[ ${#MISSING[@]} -gt 0 ]]; then
  detect_godot || fail "Godot still not found after install; set GODOT=/path/to/godot"
  info "using Godot: ${GODOT_CMD[*]}"
fi

# ------------------------------------------------------------------ import ---

info "importing the project (this generates .godot/ and builds the C# solution)"
if ! "${GODOT_CMD[@]}" --headless --path "$PROJECT_DIR" --import >/dev/null 2>&1; then
  fail "project import failed. Run it directly to see why:
       ${GODOT_CMD[*]} --headless --path '$PROJECT_DIR' --import"
fi

if ! "${GODOT_CMD[@]}" --headless --path "$PROJECT_DIR" --build-solutions --quit >/dev/null 2>&1; then
  warn "the C# solution did not build cleanly; open the project in the editor for details"
fi

info "done"
echo
echo "Next steps:"
echo "  ./run-smoke-test.sh          verify everything works (65 checks)"
echo "  ${GODOT_CMD[*]} --path '$PROJECT_DIR' --editor"
echo
echo "See CONTRIBUTING.md for the development workflow, and AGENTS.md for the"
echo "platform-specific details behind the choices this script made."
