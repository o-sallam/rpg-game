# RPG Template (Godot 4, C#/Mono)

A minimal top-down RPG starter built for Godot **4.7** with the **.NET (C#)**
build. It's a from-scratch scaffold rather than a fork of an existing
template — a survey of existing options found nothing that was both
Godot-Mono and RPG-specific (the well-known "Open RPG" demo is GDScript;
existing C# templates are generic, not RPG-oriented) — so this repo builds
that combination directly, informed by common patterns from those projects.

## Requirements

- [Godot 4.7 .NET/Mono build](https://godotengine.org/download) (the
  editor build with C# support, not the standard build)
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)

## Getting started

```bash
./bootstrap.sh        # installs Godot .NET + .NET SDK if needed, imports the project
./run-smoke-test.sh   # verifies the whole thing works (65 checks)
```

`bootstrap.sh` handles macOS, SteamOS and generic Linux, and is safe to
re-run. Use `./bootstrap.sh --check` to see what is missing without
installing anything.

Then open `project.godot` in the Godot 4.7 .NET editor and press F5. It
starts on the title screen; "Start Game" loads
`scenes/World/TestLevel.tscn`.

To set up by hand instead: open `project.godot` in the .NET editor (it
generates the C# solution on first open), build with
Project > Tools > C# > Create/Build, then press F5.

## Verifying

```bash
./run-smoke-test.sh
```

Runs the game headlessly and checks the input map, movement, collision
layers, item pickups, the dialogue signal chain, stats, inventory and
save/load — 65 checks in total. It exits non-zero on any failure, so it
works as a CI gate. The suite lives in `scripts/Tests/SmokeTest.cs`.

## Controls

| Action    | Keyboard            | Gamepad                  |
|-----------|---------------------|--------------------------|
| Move      | Arrow keys / `WASD` | Left stick / D-pad       |
| Interact  | `E`                 | `A` (bottom face button) |

Every action is a project-defined action in `project.godot` (`move_left`,
`move_right`, `move_up`, `move_down`, `interact`) rather than one of Godot's
built-in `ui_*` actions, so gameplay input and menu navigation stay
independent. Keyboard bindings use *physical* keycodes, so `WASD` lands on the
same physical keys as `ZQSD` on an AZERTY layout.

Every binding has a gamepad equivalent and matches any connected device, so
the template is playable on a Steam Deck in Game Mode with no remapping.

## Project structure

```
project.godot          Engine + autoload + input map config
RPGTemplate.csproj      C# project file (Godot.NET.Sdk, net8.0)
scripts/
  Autoload/             Singletons: GameManager (scene/pause), EventBus (global signals)
  Player/               PlayerController (movement/interaction), PlayerStats (health/leveling)
  Systems/              Inventory, InventoryItem, ItemPickup, SaveSystem, IInteractable
  NPC/                  NPC.cs (dialogue-triggering interactable)
  UI/                   HUD, DialogueBox, TitleScreen
scenes/
  TitleScreen.tscn
  Player.tscn           CharacterBody2D + Inventory + interaction Area2D
  NPC.tscn
  ItemPickup.tscn
  World/TestLevel.tscn  Playable room wiring it all together
  UI/HUD.tscn
  UI/DialogueBox.tscn
resources/items/
  health_potion.tres    Sample InventoryItem resource
```

## How the systems fit together

- **EventBus** (`scripts/Autoload/EventBus.cs`) is a global signal hub —
  `PlayerHealthChanged`, `PlayerLeveledUp`, `DialogueStarted`,
  `DialogueEnded`, `InventoryChanged`. UI listens to it instead of polling
  the player directly, so HUD/dialogue stay decoupled from gameplay code.
- **PlayerStats** is a `Resource` (health, attack, defense, leveling via
  `AddExperience`), so it can be saved, swapped, or authored as a preset.
- **Inventory** is a plain component `Node` — attach it to any actor
  (already on `Player.tscn`) and call `AddItem`/`RemoveItem`.
- **IInteractable** is implemented by `NPC.cs`; `PlayerController` tracks
  interactables in range via its `InteractionArea` and calls `Interact()`
  on the nearest one when you press `interact`. Add new interactable types
  (chests, signs, doors) by implementing the same interface.
- **SaveSystem** is a static utility (`SaveSystem.Save`/`Load`) that
  (de)serializes `PlayerStats` to `user://savegame.json`. It's not wired to
  a menu yet — call it from wherever you add save points.

## Extending

- New item: duplicate `resources/items/health_potion.tres`, change its
  `Id`/`DisplayName`/icon, point an `ItemPickup.tscn` instance at it.
- New NPC: instance `scenes/NPC.tscn`, set `NpcName`/`DialogueText` in the
  Inspector — no code needed for simple one-line dialogue.
- New enemy/combat: `PlayerStats.TakeDamage(int)` already applies defense
  mitigation and fires `PlayerHealthChanged`; hang an enemy AI + attack
  script off the same pattern.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow and the
checks required before opening a pull request. [AGENTS.md](AGENTS.md) carries
the same rules in more depth for AI coding agents, plus the macOS and SteamOS
platform notes.

## Sources consulted

- [chickensoft-games/GodotGame](https://github.com/chickensoft-games/GodotGame) — C# project template conventions (`.csproj`/SDK setup) for Godot 4
- [gdquest-demos/godot-open-rpg](https://github.com/gdquest-demos/godot-open-rpg) — reference for RPG project structure (GDScript, not C#)
- [Godot 4 C# documentation](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html) — signals, autoloads, `GlobalClass` resources
