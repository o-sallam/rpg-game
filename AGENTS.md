# AGENTS.md

Guidance for AI coding agents working in this repository. Human contributors
may find it useful too, but the audience is agents.

## Project overview

A top-down RPG starter template for **Godot 4.7** using the **.NET/Mono (C#)**
build. There is no GDScript in this project — all gameplay code is C#.

- Godot project name: `RPG Template` (matters: it determines the `user://` path)
- C# assembly name: `RPGTemplate` (`RPGTemplate.csproj`, `net8.0`)
- Main scene: `scenes/TitleScreen.tscn`
- Autoloads: `EventBus` (global signals), `GameManager` (scene changes, pause)

Target platforms are **macOS** and **SteamOS / Linux desktop** (Steam Deck).
Windows is not a runtime target and is not tested; do not add Windows-only
APIs or path assumptions. **File and directory names are held to a stricter,
fully platform-agnostic standard than the runtime targets** — including
Windows and exFAT constraints — because a name that is merely macOS-and-Linux
safe still breaks contributor checkouts, CI runners, and microSD copies. Those
rules are mandatory and are specified below.

## Repository layout

```
project.godot          Engine config, autoloads, input map, collision layer names
RPGTemplate.csproj     Godot.NET.Sdk, net8.0 (Godot rewrites the SDK version)
bootstrap.sh           One-shot dev environment setup for macOS / Linux / SteamOS
run-smoke-test.sh      Builds and runs the headless smoke test; CI-usable exit code
CONTRIBUTING.md        Human-facing contributor guide (this file is its agent twin)
scripts/
  Autoload/            EventBus, GameManager (registered in project.godot)
  Player/              PlayerController, PlayerStats
  Systems/             Inventory, InventoryItem, InventorySlot, ItemPickup,
					   SaveSystem, IInteractable
  NPC/                 NPC.cs
  UI/                  HUD, DialogueBox, TitleScreen
  Tests/               SmokeTest.cs
scenes/                Player, NPC, ItemPickup, TitleScreen, SmokeTest, UI/, World/
resources/items/       Sample InventoryItem .tres resources
```

## Environment setup

`./bootstrap.sh` automates everything below: it detects macOS / Linux / SteamOS,
installs the Godot .NET editor and a .NET 8 SDK if missing, and imports the
project. `./bootstrap.sh --check` reports what is missing without installing.
The rest of this section is what it does and why, for when it does not fit.

### macOS

```bash
brew install --cask godot-mono     # Godot 4.7 .NET editor (requires macOS 11+)
brew install --cask dotnet-sdk     # .NET 8 SDK or newer
```

- **Use the `godot-mono` cask, not `godot`.** The plain `godot` cask has no C#
  support and will fail to load every script in this project.
- The cask installs an app bundle into `/Applications`. Confirm its exact name
  before scripting against it — it has changed between releases:
  ```bash
  ls /Applications | grep -i godot
  GODOT="/Applications/Godot_mono.app/Contents/MacOS/Godot"   # adjust to match
  ```
- **Apple Silicon:** install the arm64 .NET SDK. A Rosetta/x64 `dotnet` paired
  with an arm64 Godot produces confusing native-load failures at runtime, not
  at build time. Check with `dotnet --info | grep -i architecture`.
- If a manually downloaded (non-Homebrew) Godot refuses to launch, clear the
  quarantine attribute: `xattr -dr com.apple.quarantine /Applications/<bundle>.app`.
  Homebrew casks normally handle this already.
- `user://` resolves to
  `~/Library/Application Support/Godot/app_userdata/RPG Template/`.
  `SaveSystem` writes `savegame.json` there.

### SteamOS (Steam Deck)

SteamOS 3.x is Arch-based with an **immutable, read-only root filesystem**.
Do not use `pacman` to install the editor or the .NET SDK — those changes are
wiped by the next SteamOS update, and `sudo steamos-readonly disable` is not
something to do on a user's machine. Use Flatpak, which persists across
updates.

In **Desktop Mode**:

```bash
flatpak install --user flathub org.godotengine.GodotSharp
flatpak run org.godotengine.GodotSharp
```

- Use `org.godotengine.GodotSharp`, **not** `org.godotengine.Godot` — the
  latter is the GDScript-only build. (Both Flatpaks are community-maintained,
  not official Godot builds.)
- The GodotSharp Flatpak bundles its own .NET 8 SDK, so no separate install is
  needed. In `Editor Settings > Dotnet > Builds`, make sure the build tool is
  **dotnet CLI**, not MSBuild (Mono).
- **Flatpak sandboxing** affects both ends of the filesystem:
  - The editor can only reach the project if it is under `$HOME` or you grant
    access explicitly (Flatseal, or `--filesystem=<path>`).
  - `user://` is redirected into the sandbox at
    `~/.var/app/org.godotengine.GodotSharp/data/godot/app_userdata/RPG Template/`.
	When a save file "doesn't appear" on the Deck, look there before assuming
    `SaveSystem` is broken.
- Outside Flatpak (plain Linux), `user://` is
  `~/.local/share/godot/app_userdata/RPG Template/`.

## Build and run

Preferred: open `project.godot` in the Godot .NET editor and press F5. Godot
builds the C# solution automatically and reports script errors in the editor,
which `dotnet build` alone will not catch.

Command line:

```bash
dotnet build                                  # compile C# only
"$GODOT" --headless --import                  # import assets, populate .godot/
"$GODOT" --headless --build-solutions --quit  # import + build the solution
"$GODOT" --headless res://scenes/AScene.tscn  # run a scene with no display
```

Note that `--build-solutions` combined with a scene path hangs; build first,
then run as a separate command.

### Running headless on a Linux agent or CI box

You are not stuck with static checks — a full headless run is available on a
plain Ubuntu container, and is expected before claiming a change works:

```bash
apt-get install -y dotnet-sdk-8.0     # Ubuntu 24.04 ships 8.0 in noble-updates/main
curl -sSL -o godot.zip https://github.com/godotengine/godot/releases/download/\
4.7.1-stable/Godot_v4.7.1-stable_mono_linux_x86_64.zip
unzip -q godot.zip
```

Microsoft's own `dot.net` / `builds.dotnet.microsoft.com` endpoints may be
blocked by an egress proxy; the Ubuntu package is the reliable route. The mono
build **hard-requires** the .NET runtime — without it Godot segfaults on
startup rather than degrading gracefully, so install the SDK first.

### The smoke test

`scripts/Tests/SmokeTest.cs`, run via `scenes/SmokeTest.tscn`, is the project's
test suite. Run it with:

```bash
./run-smoke-test.sh          # honours $GODOT; finds brew/flatpak installs otherwise
```

It exits 0 only when every check passes, so it works directly as a CI gate. It
covers what compilation cannot: collision layer/mask pairings, `NodePath`
exports pointing at the wrong node, signals that never reach a listener, and
input actions that do not actually drive anything.

**"Verified" means the solution built with no errors *and* `./run-smoke-test.sh`
passed** — not that `dotnet build` exited 0.

When you add a system, add checks for it. Keep the existing shape: a
`Check(bool, string)` call per assertion, grouped into a `CheckX()` method with
a `GD.Print` header. The live-level section at the end is the valuable part —
it instantiates `TestLevel.tscn` and drives the real game, so prefer extending
that over adding more static assertions.

Two constraints on anything you add there:

- **Leave no state behind.** The suite writes a real save file, so it backs up
  and restores any pre-existing `user://savegame.json`. Anything else that
  touches `user://` must do the same — a test that eats a player's save is
  worse than no test.
- **Unsubscribe from `EventBus`.** The autoload outlives the test scene, so use
  named local functions and detach them, as the existing checks do.

If you genuinely cannot launch Godot, say so explicitly rather than implying
the change was tested, and fall back to static checks: `.tscn`/`.tres` files
must have `load_steps` equal to the number of `ext_resource` + `sub_resource`
entries plus one, every `ExtResource("id")` / `SubResource("id")` must resolve
to a declared id, and every `NodePath("...")` must match a node actually
declared in that scene.

### Generated files

Godot writes several files on first import. `*.uid` (one per script) and
`*.import` (one per asset) **are tracked on purpose** — they keep `uid://`
references and import settings stable across machines. `.godot/`, `bin/`,
`obj/` and `*.csproj.old` are ignored. Godot also rewrites the `Godot.NET.Sdk`
version in `RPGTemplate.csproj` to match the engine that opened the project;
that change is expected, keep it.

## File and directory naming — strict, non-negotiable

Every file and directory name in this repository must be portable across
**every** filesystem the project touches: case-insensitive APFS on macOS,
case-sensitive ext4/btrfs on SteamOS, exFAT on a Steam Deck microSD card, and
NTFS on any contributor's machine. Names are therefore restricted to the
lowest common denominator of all of them. This is not a style preference — the
excluded characters cause silent data loss, unreproducible build failures, or
files that cannot be checked out at all.

### The allowed set

Every path component (each directory name and each filename, including its
extension) **must** match this regex, and nothing outside it is permitted:

```
^[A-Za-z0-9][A-Za-z0-9._-]*$
```

In words: ASCII letters and digits, plus `.` `_` `-`, and it must start with a
letter or digit. The only exception is a deliberate dotfile or dot-directory at
the repository root (`.gitignore`, `.github/`), which may take one leading dot.

### Explicitly forbidden

Do not create, rename to, or reference any name containing:

- **Spaces.** Break unquoted shell in build and export scripts. Use `-` or `_`.
- **Any non-ASCII character** — accented letters, CJK, emoji, curly quotes.
  macOS normalizes filenames to Unicode NFD while Linux stores NFC, so the same
  visible name becomes two different byte sequences. Git then reports a file
  that is simultaneously deleted and untracked, and the file silently fails to
  load on one of the two platforms.
- `< > : " / \ | ? *` and any control character (0x00–0x1F). Reserved on
  NTFS/exFAT; `:` additionally carries legacy path-separator meaning on macOS.
- `# % & { } $ ! ' @ + = ~ ^` and backtick. These survive on disk but break
  shell globbing, `res://` URI parsing, and Godot's `.import` bookkeeping.
- A **leading hyphen** (`-foo.tscn`), which CLI tools parse as a flag.
- A **trailing dot or trailing space** (`Player.tscn.`). Windows silently
  strips them, so the checked-out name stops matching the committed name.
- The **Windows reserved device names**, with or without an extension:
  `CON`, `PRN`, `AUX`, `NUL`, `COM0`–`COM9`, `LPT0`–`LPT9`. `NUL.cs` cannot be
  created on Windows at all, which makes the repo un-clonable there.
- **Two names in the same directory differing only by case** (`Player.cs` and
  `player.cs`). They coexist on SteamOS and collapse into one file on macOS,
  destroying one of them on checkout.

Keep each component under 255 bytes and total paths short.

### Required casing per file type

Casing is part of the name. Match the existing convention exactly:

| Kind | Convention | Example |
|------|-----------|---------|
| Top-level directories | lowercase | `scripts/`, `scenes/`, `resources/` |
| Nested directories | PascalCase | `scripts/Systems/`, `scenes/UI/` |
| C# scripts | PascalCase, matching the primary type name | `PlayerController.cs` |
| Scenes (`.tscn`) | PascalCase, matching the root node name | `ItemPickup.tscn` |
| Resources (`.tres`) | snake_case | `health_potion.tres` |
| Root config and docs | whatever the tool requires | `project.godot`, `README.md` |

Do not rename existing files to "harmonize" these conventions unless asked to;
a rename that changes only case needs a two-step `git mv` to survive a
case-insensitive checkout, and it invalidates every `res://` reference and
`.uid` mapping pointing at the old name.

### Referencing paths from code and scenes

- Every `res://` and `user://` string must match the on-disk name **exactly**,
  byte for byte, including case. **Godot's exported PCK is case-sensitive on
  every platform**, so a mis-cased path can load correctly in the editor on
  macOS and still fail in the exported build on that same Mac. Passing on your
  machine proves nothing here.
- Never assemble game-data paths with `\` or `System.IO`; see the rule below.

### Verifying before you commit

These three checks must all come back empty. Run them from the repo root:

```bash
# 1. Any path component outside the allowed set
git ls-files | tr '/' '\n' | sort -u | grep -Ev '^\.?[A-Za-z0-9][A-Za-z0-9._-]*$'

# 2. Names colliding under case folding
git ls-files | tr 'A-Z' 'a-z' | sort | uniq -d

# 3. Windows reserved device names
git ls-files | tr '/' '\n' | sed 's/\..*//' | tr 'a-z' 'A-Z' \
  | grep -Ex 'CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9]'
```

The repository currently passes all three. If you add files, it must still
pass — a violation is a blocking defect, not a nit to clean up later.

## Cross-platform rules

These are the failure modes that actually bite this project:

1. **Filesystem case sensitivity.** macOS APFS is case-insensitive by default;
   SteamOS (ext4/btrfs) is case-sensitive. A `res://Scenes/player.tscn`
   reference that loads fine on a Mac will fail to load on the Deck. Always
   match the on-disk casing exactly — see the naming rules above. This is the
   single most common way a macOS-authored change breaks on SteamOS.
2. **Never build paths with `\` or `System.IO` for game data.** Use `res://`
   and `user://` with Godot's `FileAccess`/`DirAccess`, as `SaveSystem` does.
3. **Line endings and file modes.** Exported Linux binaries need the executable
   bit. If a build is copied to a microSD card formatted exFAT, the exec bit is
   lost — `chmod +x` after copying.
4. **Do not hardcode a window size to one device.** The Steam Deck panel is
   1280×800 (16:10). If you touch display settings, keep the UI resizable and
   check both a 16:10 and a 16:9 aspect.

## Input and controller support

All gameplay input goes through project-defined actions in `project.godot`.
Nothing in gameplay depends on Godot's built-in `ui_*` actions:

| Action | Keyboard (physical) | D-pad | Stick |
|--------|--------------------|-------|-------|
| `move_left` | Left arrow, `A` | `JOY_BUTTON_DPAD_LEFT` (13) | axis 0 @ -1.0 |
| `move_right` | Right arrow, `D` | `JOY_BUTTON_DPAD_RIGHT` (14) | axis 0 @ +1.0 |
| `move_up` | Up arrow, `W` | `JOY_BUTTON_DPAD_UP` (11) | axis 1 @ -1.0 |
| `move_down` | Down arrow, `S` | `JOY_BUTTON_DPAD_DOWN` (12) | axis 1 @ +1.0 |
| `interact` | `E` | `JOY_BUTTON_A` (0) | — |

`PlayerController` reads movement with
`Input.GetVector("move_left", "move_right", "move_up", "move_down")`.

Conventions to preserve when adding or reworking controls:

- **Keep gameplay off `ui_*`.** Those are for menu navigation; sharing them
  means a menu that navigates while the player is still walking around. Add a
  named action instead.
- **Give every gameplay action a joypad binding at the point you add it**, not
  later. The Deck in Game Mode has no keyboard, so a keyboard-only action is
  unreachable there without a Steam Input remap — a bug, not a configuration
  step to push onto the player.
- **Use `physical_keycode`, not `keycode`.** Physical codes bind to key
  position, so `WASD` automatically becomes `ZQSD` on AZERTY. Every keyboard
  event in this project already does this.
- **Every event uses `"device": -1`**, matching any connected controller rather
  than a fixed index. Keep it that way — a hardcoded index breaks when the Deck
  enumerates its built-in pad differently from a docked external one.
- Movement actions use a `0.2` deadzone (analog sticks need to stay responsive
  near centre); `interact` keeps Godot's `0.5` default, which is fine for a
  digital button.

## Code style

Match the existing code; it is consistent and deliberate:

- C# scripts attached to nodes are `public partial class X : <GodotType>`.
- Exported members use `[Export] public T Name { get; set; }` in PascalCase.
  **The property name in the C# file and the key in the `.tscn` file must match
  exactly** — a rename in one place silently breaks the other, and Godot will
  not warn you. Grep the `.tscn` files after renaming any exported property.
- Cross-system communication goes through `EventBus` signals, not direct node
  references. UI listens to `EventBus`; it does not poll the player.
- Subscribe to `EventBus` in `_Ready()` and **always unsubscribe in
  `_ExitTree()`** (see `HUD.cs`, `DialogueBox.cs`). The autoload outlives
  scenes, so a missed unsubscribe leaks into the next scene and fires callbacks
  on freed nodes.
- Guard autoload access with `?.` (`EventBus.Instance?.EmitSignal(...)`) —
  resources and tool scripts can run without the tree.
- Prefer `Resource` subclasses (`PlayerStats`, `InventoryItem`) with
  `[GlobalClass]` for authored data, so it is editable in the Inspector.
- Plain data that never enters the scene tree stays a plain C# class
  (`InventorySlot`) — do not derive from `Node`/`Resource` without reason.
- Comments explain *why*, sparingly. Do not add narration to obvious code.

## Do not commit

Already covered by `.gitignore`, but worth stating: never commit `.godot/`,
`bin/`, `obj/`, `.mono/`, `export_presets.cfg` (it can contain signing
identities and store credentials), `.DS_Store`, or `*.user` files. If you add
an export preset, do not commit macOS signing identities, notarization Apple
IDs, or app-specific passwords.

## Commits and pull requests

- Develop on the designated feature branch; never push directly to `main`.
- Write commit messages that explain the reasoning, not just the file list.
- Do not open a pull request unless explicitly asked.
- Do not reference the specific AI model used in commits, PRs, or code
  comments.
