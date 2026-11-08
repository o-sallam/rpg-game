# Contributing

Thanks for taking an interest. This is a **starter template**, not a game, and
that shapes what belongs here: changes should make the scaffolding clearer or
more useful to someone starting a project, rather than turning it into a
specific game. A generic inventory system fits; a boss fight does not.

If you are an AI coding agent, read [AGENTS.md](AGENTS.md) instead — it carries
the same rules in more detail, plus the constraints that matter when you cannot
open the editor.

## Getting set up

```bash
./bootstrap.sh
```

It detects macOS or Linux/SteamOS, installs the Godot .NET editor and a .NET 8
SDK if they are missing, and imports the project. Re-running it is safe.
`./bootstrap.sh --check` reports what is missing without installing anything.

If you would rather do it by hand, see the setup sections in
[AGENTS.md](AGENTS.md) — in particular the two traps worth knowing up front:

- **Install the .NET build of Godot**, not the plain one. Everything here is
  C#, and the plain build silently fails to load all of it.
- **On SteamOS, use the Flatpak**, not `pacman`. The root filesystem is
  read-only, and package changes are wiped by the next system update.

## The development loop

```bash
./run-smoke-test.sh                     # build + verify (65 checks, exits non-zero on failure)
godot --path . --editor                 # open the editor; F5 runs the game
```

Prefer running the game in the editor while iterating — Godot surfaces script
and scene errors there that a plain `dotnet build` will not catch.

## Before you open a pull request

Two checks are required. Both are quick.

**1. The smoke test passes.**

```bash
./run-smoke-test.sh
```

This exercises the real game headlessly: the input map, movement, collision
layers, pickups, the dialogue signal chain, stats, inventory and save/load. It
catches the class of bug that compiles perfectly and is still broken — a
`NodePath` export pointing at the wrong node, a collision mask that no longer
overlaps, a signal nobody receives.

If you add a system, add checks for it in `scripts/Tests/SmokeTest.cs`. A
change that cannot be exercised there is worth a note in the PR explaining why.

**2. File names pass the portability checks.**

```bash
git ls-files | tr '/' '\n' | sort -u | grep -Ev '^\.?[A-Za-z0-9][A-Za-z0-9._-]*$'
git ls-files | tr 'A-Z' 'a-z' | sort | uniq -d
```

Both must print nothing. Naming is held to a strict, fully platform-agnostic
standard — see the naming section of [AGENTS.md](AGENTS.md) for the rules and
the reasoning. The short version: ASCII letters, digits, `.`, `_` and `-` only,
no spaces, and never two names differing only in case.

This is stricter than it looks like it needs to be, for a concrete reason:
macOS is case-insensitive and SteamOS is not, so a path that works on your Mac
can fail on a Steam Deck — and Godot's exported PCK is case-sensitive on
*every* platform, so a mis-cased `res://` path can even work in your editor and
break in your own exported build.

## Code style

Match the surrounding code. The full set is in [AGENTS.md](AGENTS.md); the
points that come up most often:

- Node scripts are `public partial class X : <GodotType>`.
- Exported members are `[Export] public T Name { get; set; }` in PascalCase.
  **The C# property name and the `.tscn` key must match exactly** — Godot will
  not warn you when they drift, so grep the scenes after renaming one.
- Systems talk to each other through `EventBus` signals, not direct node
  references. UI listens; it does not poll.
- Subscribe in `_Ready()`, and **always** unsubscribe in `_ExitTree()`. The
  autoload outlives scenes, so a missed unsubscribe fires callbacks on freed
  nodes in the next scene.
- Every new gameplay action needs a **gamepad binding when you add it**. A
  keyboard-only action is unreachable on a Steam Deck in Game Mode, which is a
  bug rather than something to leave to the player's remapping.

## Commits and pull requests

- Branch off `main`; do not push to it directly.
- Write commit messages that explain *why*, not just what changed. The diff
  already says what changed.
- Keep a pull request to one coherent change.
- Say what you actually verified. "Smoke test passes" and "opened it on a Deck
  and played it" are different claims, and the difference matters here — most
  of this project has never touched real hardware.

## Testing on the target platforms

Automated checks run headlessly on Linux, which covers logic, collision and
signals. They cannot tell you how the game *feels*. If you have the hardware,
these are genuinely useful things to report in a PR:

- Stick and D-pad response on a Steam Deck in Game Mode
- Whether the UI reads well at the Deck's 1280×800 (16:10) panel
- macOS behaviour on Apple Silicon, especially an exported `.app`

## Reporting bugs

Open an issue with the platform, the Godot version (`godot --version`), the
.NET SDK version (`dotnet --version`), and whether `./run-smoke-test.sh`
passes. That last one separates "the template is broken" from "my environment
is broken" immediately.
