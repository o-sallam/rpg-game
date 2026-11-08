using Godot;
using System.Linq;

// Headless smoke test for the template. Run it with:
//
//     ./run-smoke-test.sh
//
// or directly:
//
//     godot --headless res://scenes/SmokeTest.tscn
//
// Exits 0 when every check passes, 1 otherwise, so it works as a CI gate.
//
// This deliberately covers what compiling the project cannot: collision
// layer/mask values, NodePath exports pointing at the wrong node, signals
// that never reach their listener, and input actions that do not actually
// drive anything. Add a check here whenever you add a system.
public partial class SmokeTest : Node
{
    private const string SavePath = "user://savegame.json";

    private int _pass;
    private int _fail;
    private bool _hadExistingSave;
    private string _existingSave;

    private void Check(bool condition, string label)
    {
        if (condition)
        {
            _pass++;
            GD.Print($"  ok    {label}");
        }
        else
        {
            _fail++;
            GD.Print($"  FAIL  {label}");
        }
    }

    public override async void _Ready()
    {
        BackUpExistingSave();

        CheckInputMap();
        CheckStats();
        CheckInventory();
        CheckSaveLoad();
        CheckScenesLoad();
        CheckHealthComponent();
        CheckAbilityCooldowns();
        await CheckLiveLevel();
        await CheckLive3DPrototype();

        RestoreExistingSave();

        GD.Print($"\n==== {_pass} passed, {_fail} failed ====");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }

    // The test writes a real save file. Preserve whatever was already there so
    // running the suite never destroys a player's progress.
    private void BackUpExistingSave()
    {
        _hadExistingSave = FileAccess.FileExists(SavePath);
        if (!_hadExistingSave)
        {
            return;
        }

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        _existingSave = file.GetAsText();
    }

    private void RestoreExistingSave()
    {
        if (_hadExistingSave)
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            file.StoreString(_existingSave);
            return;
        }

        DirAccess.Open("user://")?.Remove("savegame.json");
    }

    private void CheckInputMap()
    {
        GD.Print("\ninput map");

        foreach (var action in new[] { "interact", "move_left", "move_right", "move_up", "move_down" })
        {
            Check(InputMap.HasAction(action), $"action exists: {action}");
        }

        Check(InputMap.HasAction("move_to_click"), "action exists: move_to_click");
        Check(InputMap.ActionGetEvents("move_to_click")
                .Any(e => e is InputEventMouseButton m && m.ButtonIndex == MouseButton.Right),
            "move_to_click is bound to the right mouse button");

        // Left click stays unbound: it is reserved, not used by the prototype.
        Check(InputMap.GetActions().Cast<StringName>()
                .Where(a => !a.ToString().StartsWith("ui_"))
                .SelectMany(a => InputMap.ActionGetEvents(a))
                .All(e => e is not InputEventMouseButton m || m.ButtonIndex != MouseButton.Left),
            "no gameplay action is bound to the left mouse button");

        foreach (var (action, key) in new[]
        {
            ("ability_q", Key.Q), ("ability_w", Key.W), ("ability_e", Key.E), ("ability_r", Key.R),
        })
        {
            Check(InputMap.HasAction(action), $"action exists: {action}");
            var events = InputMap.ActionGetEvents(action);
            Check(events.Any(e => e is InputEventKey k && k.PhysicalKeycode == key),
                $"{action} is bound to physical {key}");
            Check(events.Any(e => e is InputEventJoypadButton), $"{action} has a gamepad binding");
            Check(events.All(e => e.Device == -1), $"{action} events all use device -1");
        }

        var interact = InputMap.ActionGetEvents("interact");
        Check(interact.Any(e => e is InputEventKey k && (int)k.PhysicalKeycode == 69),
            "interact is bound to physical E");
        Check(interact.Any(e => e is InputEventJoypadButton b && (int)b.ButtonIndex == 0),
            "interact is bound to joypad button 0 (A)");

        // Every gameplay action needs a gamepad route, or the Steam Deck in
        // Game Mode cannot reach it. See AGENTS.md.
        foreach (var action in new[] { "interact", "move_left", "move_right", "move_up", "move_down" })
        {
            var events = InputMap.ActionGetEvents(action);
            Check(events.Any(e => e is InputEventJoypadButton or InputEventJoypadMotion),
                $"{action} has a gamepad binding");
            Check(events.All(e => e.Device == -1), $"{action} events all use device -1");
        }

        foreach (var action in new[] { "move_left", "move_right", "move_up", "move_down" })
        {
            var events = InputMap.ActionGetEvents(action);
            Check(events.Count(e => e is InputEventKey) == 2, $"{action} has arrow + WASD keys");
            Check(events.Any(e => e is InputEventJoypadMotion), $"{action} has a stick axis");
            Check(events.OfType<InputEventKey>().All(k => k.PhysicalKeycode != Key.None),
                $"{action} keys use physical keycodes");
        }
    }

    private void CheckStats()
    {
        GD.Print("\nstats");

        var stats = new PlayerStats();
        stats.TakeDamage(20);
        Check(stats.CurrentHealth == 85, "TakeDamage(20) against Defense 5 leaves 85 HP");
        stats.TakeDamage(1);
        Check(stats.CurrentHealth == 84, "damage below Defense still costs at least 1 HP");
        stats.Heal(999);
        Check(stats.CurrentHealth == 100, "Heal clamps to MaxHealth");
        stats.TakeDamage(9999);
        Check(stats.CurrentHealth == 0 && !stats.IsAlive, "health floors at 0 and IsAlive goes false");

        var leveling = new PlayerStats();
        leveling.AddExperience(100);
        Check(leveling.Level == 2 && leveling.MaxHealth == 120 && leveling.CurrentHealth == 120,
            "AddExperience(100) levels up and restores health");
        Check(leveling.ExperienceToNextLevel == 125, "XP requirement scales to 125");

        var multi = new PlayerStats();
        multi.AddExperience(1000);
        Check(multi.Level > 2, $"a large XP grant cascades multiple levels (reached {multi.Level})");
    }

    private void CheckInventory()
    {
        GD.Print("\ninventory");

        var inventory = new Inventory();
        AddChild(inventory);

        var potion = GD.Load<InventoryItem>("res://resources/items/health_potion.tres");
        Check(potion != null && potion.Id == "health_potion", "health_potion.tres loads with its Id");

        inventory.AddItem(potion, 3);
        Check(inventory.Slots.Count == 1 && inventory.Slots[0].Quantity == 3, "AddItem stacks to 3");
        inventory.RemoveItem("health_potion", 2);
        Check(inventory.Slots[0].Quantity == 1, "RemoveItem(2) leaves 1");
        inventory.RemoveItem("health_potion", 1);
        Check(inventory.Slots.Count == 0, "emptying a stack drops the slot");
        Check(!inventory.RemoveItem("nope", 1), "removing an absent item fails cleanly");
        Check(!inventory.AddItem(null), "AddItem(null) is rejected");

        inventory.QueueFree();
    }

    private void CheckSaveLoad()
    {
        GD.Print("\nsave/load");

        var stats = new PlayerStats { CharacterName = "Tester" };
        stats.AddExperience(100);
        SaveSystem.Save(stats);

        var loaded = SaveSystem.Load();
        Check(loaded != null, "Load returns a result after Save");
        Check(loaded?.CharacterName == "Tester", "character name round-trips");
        Check(loaded?.Level == stats.Level && loaded?.MaxHealth == stats.MaxHealth
              && loaded?.ExperienceToNextLevel == stats.ExperienceToNextLevel,
            "level, health and XP curve round-trip");
    }

    private void CheckHealthComponent()
    {
        GD.Print("\nhealth component");

        var health = new HealthComponent { MaxHealth = 50 };
        AddChild(health);

        Check(health.CurrentHealth == 50 && health.IsAlive, "starts at MaxHealth and alive");

        var reported = -1;
        var deaths = 0;
        void OnChanged(int current, int _) => reported = current;
        void OnDied() => deaths++;
        health.HealthChanged += OnChanged;
        health.Died += OnDied;

        health.TakeDamage(20);
        Check(health.CurrentHealth == 30, "TakeDamage(20) leaves 30 (no defence reduction here)");
        Check(reported == 30, "HealthChanged reports the new value");

        health.Heal(999);
        Check(health.CurrentHealth == 50, "Heal clamps to MaxHealth");

        health.TakeDamage(0);
        health.TakeDamage(-5);
        Check(health.CurrentHealth == 50, "zero and negative damage are ignored");

        health.TakeDamage(9999);
        Check(health.CurrentHealth == 0 && !health.IsAlive, "health floors at 0 and IsAlive goes false");
        Check(deaths == 1, "Died fires exactly once");

        health.TakeDamage(10);
        health.Heal(10);
        health.Die();
        Check(health.CurrentHealth == 0 && deaths == 1, "a corpse cannot be damaged, healed or killed again");

        health.HealthChanged -= OnChanged;
        health.Died -= OnDied;
        health.QueueFree();
    }

    private void CheckAbilityCooldowns()
    {
        GD.Print("\nability cooldowns");

        var casts = 0;
        var ability = new Ability("ability_q", "Q", "Test", 5.0f, () => casts++);

        Check(ability.CanUse() && ability.IsReady, "a fresh ability is ready");
        Check(ability.Use() && casts == 1, "Use runs the effect");
        Check(!ability.CanUse(), "it is unavailable straight after casting");
        Check(Mathf.IsEqualApprox(ability.CurrentCooldown, 5.0f), "the full cooldown is applied");
        Check(!ability.Use() && casts == 1, "casting on cooldown does nothing");

        ability.Tick(2.0f);
        Check(Mathf.IsEqualApprox(ability.CurrentCooldown, 3.0f), "Tick counts the cooldown down");

        ability.Tick(99.0f);
        Check(ability.CurrentCooldown == 0.0f, "the cooldown floors at 0 rather than going negative");
        Check(ability.Use() && casts == 2, "it can be cast again once ready");

        ability.ResetCooldown();
        Check(ability.IsReady, "ResetCooldown clears it");
    }

    private void CheckScenesLoad()
    {
        GD.Print("\nscenes");

        foreach (var path in new[]
        {
            "res://scenes/TitleScreen.tscn",
            "res://scenes/Player.tscn",
            "res://scenes/NPC.tscn",
            "res://scenes/ItemPickup.tscn",
            "res://scenes/UI/HUD.tscn",
            "res://scenes/UI/DialogueBox.tscn",
            "res://scenes/World/TestLevel.tscn",
            "res://scenes/Player3D.tscn",
            "res://scenes/Monster.tscn",
            "res://scenes/UI/HealthBar3D.tscn",
            "res://scenes/UI/AbilityBar.tscn",
            "res://scenes/World/Main3D.tscn",
        })
        {
            Check(GD.Load<PackedScene>(path) != null, $"loads: {path}");
        }

        Check(GD.Load<PackedScene>(ProjectSettings.GetSetting("application/run/main_scene").AsString()) != null,
            "the configured main scene loads");
    }

    // Everything above is static. This section runs the actual game.
    private async System.Threading.Tasks.Task CheckLiveLevel()
    {
        GD.Print("\nlive level");

        var level = GD.Load<PackedScene>("res://scenes/World/TestLevel.tscn").Instantiate();
        AddChild(level);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var player = level.GetNodeOrNull<PlayerController>("Player");
        var npc = level.GetNodeOrNull<NPC>("NPC");
        var pickup = level.GetNodeOrNull<ItemPickup>("HealthPotionPickup");

        Check(player != null, "Player is present");
        Check(npc != null, "NPC is present");
        Check(pickup != null, "ItemPickup is present");
        Check(level.GetNodeOrNull("HUD") != null && level.GetNodeOrNull("DialogueBox") != null,
            "HUD and DialogueBox are present");

        if (player == null || npc == null || pickup == null)
        {
            GD.Print("  (skipping live checks: the level is missing nodes)");
            return;
        }

        var inventory = player.GetNodeOrNull<Inventory>("Inventory");
        Check(inventory != null, "Player has an Inventory child");

        // Movement: proves the move_* actions actually reach the controller.
        var startX = player.GlobalPosition.X;
        Input.ActionPress("move_right");
        await WaitPhysics(10);
        Input.ActionRelease("move_right");
        Check(player.GlobalPosition.X > startX + 1f,
            $"move_right drives the player (x {startX:0.#} -> {player.GlobalPosition.X:0.#})");

        // Walls: proves the level's collision bodies actually block movement.
        player.GlobalPosition = new Vector2(60, 300);
        Input.ActionPress("move_left");
        await WaitPhysics(30);
        Input.ActionRelease("move_left");
        Check(player.GlobalPosition.X > 10f,
            $"the left wall stops the player (x {player.GlobalPosition.X:0.#})");

        // Pickup: proves the collision layer/mask pairing between the player
        // body and the pickup area.
        player.GlobalPosition = pickup.GlobalPosition;
        await WaitPhysics(10);
        Check(inventory != null && inventory.Slots.Count == 1, "walking onto ItemPickup grants the item");
        Check(!IsInstanceValid(pickup) || pickup.IsQueuedForDeletion(), "the pickup frees itself");

        // Dialogue: proves NPC -> EventBus -> DialogueBox is wired end to end.
        var dialogueFired = false;
        void OnDialogue(string _, string __) => dialogueFired = true;
        EventBus.Instance.DialogueStarted += OnDialogue;
        npc.Interact(player);
        Check(dialogueFired, "NPC.Interact emits DialogueStarted");
        Check(GetTree().Paused, "DialogueBox pauses the tree while open");
        EventBus.Instance.DialogueStarted -= OnDialogue;
        GetTree().Paused = false;

        // Health signal: proves stats changes reach listeners such as the HUD.
        var reported = -1;
        void OnHealth(int current, int _) => reported = current;
        EventBus.Instance.PlayerHealthChanged += OnHealth;
        player.Stats.TakeDamage(30);
        Check(reported == player.Stats.CurrentHealth, "TakeDamage propagates over EventBus");
        EventBus.Instance.PlayerHealthChanged -= OnHealth;

        level.QueueFree();
    }

    // The 3D click-to-move prototype: proves the EventBus hop from a ground
    // click to the controller, plus arrival and facing.
    private async System.Threading.Tasks.Task CheckLive3DPrototype()
    {
        GD.Print("\nlive 3D prototype");

        var world = GD.Load<PackedScene>("res://scenes/World/Main3D.tscn").Instantiate();
        AddChild(world);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var player = world.GetNodeOrNull<PlayerController3D>("Player3D");
        var camera = world.GetNodeOrNull<IsometricCamera>("IsometricCamera");
        var detector = world.GetNodeOrNull<GroundClickDetector>("GroundClickDetector");
        var animationPlayer = world.GetNodeOrNull<AnimationPlayer>("Player3D/Visual/Fox/AnimationPlayer");

        Check(player != null, "Player3D is present");
        Check(camera != null, "IsometricCamera is present");
        Check(detector != null, "GroundClickDetector is present");
        Check(world.GetNodeOrNull("Environment/Ground") != null, "Ground is present");
        Check(world.GetNodeOrNull("Lighting/DirectionalLight3D") != null, "DirectionalLight3D is present");
        Check(world.GetNodeOrNull("WorldEnvironment") != null, "WorldEnvironment is present");

        // The Fox model is a child visual, not a replacement for the body.
        Check(world.GetNodeOrNull<Node3D>("Player3D/Visual/Fox") != null, "the Fox model hangs under Player3D/Visual");
        Check(world.GetNodeOrNull<CollisionShape3D>("Player3D/CollisionShape3D") != null,
            "the collider stays on the CharacterBody3D");
        Check(animationPlayer != null, "the Fox brings an AnimationPlayer");

        if (animationPlayer != null)
        {
            foreach (var clip in new[] { "Survey", "Walk" })
            {
                Check(animationPlayer.HasAnimation(clip), $"Fox.glb contains the {clip} clip");
                Check(animationPlayer.GetAnimation(clip).LoopMode == Animation.LoopModeEnum.Linear,
                    $"{clip} is imported as looping");
            }
        }

        if (player == null || camera == null)
        {
            GD.Print("  (skipping live 3D checks: the scene is missing nodes)");
            return;
        }

        Check(camera.Projection == Camera3D.ProjectionType.Orthogonal, "the camera is orthographic");

        // The mouse-to-world ray, exercised through the real camera projection.
        // The player sits under the screen centre, so a ray there must land on
        // the ground close to the player's feet.
        if (detector != null)
        {
            var centre = GetViewport().GetVisibleRect().Size * 0.5f;
            var gotPoint = detector.TryGetGroundPoint(centre, out var groundPoint);
            Check(gotPoint, "a ray through the screen centre hits the ground");
            Check(gotPoint && Mathf.Abs(groundPoint.Y) < 0.2f,
                $"the hit lands on the ground plane (y {groundPoint.Y:0.##})");
            Check(gotPoint && new Vector2(groundPoint.X, groundPoint.Z).Length() < 2.0f,
                $"the hit is under the camera focus (x {groundPoint.X:0.##}, z {groundPoint.Z:0.##})");
        }

        // The player is on layer 2 and the ground on layer 1, so a ground ray
        // cannot be blocked by the player itself.
        Check(player.CollisionLayer == 2 && player.CollisionMask == 1,
            "Player3D is on the player layer and collides with world");

        // Gravity + the ground collider should settle the capsule, not drop it.
        await WaitPhysics(20);
        Check(player.IsOnFloor(), $"the player rests on the ground (y {player.GlobalPosition.Y:0.##})");

        // The model is authored in centimetres; the Visual scale has to bring it
        // down to roughly the capsule's size or the world scale looks wrong.
        var fox = world.GetNodeOrNull<MeshInstance3D>("Player3D/Visual/Fox/root/Skeleton3D/fox");
        Check(fox != null, "the Fox mesh instance is reachable");
        if (fox != null)
        {
            var bounds = fox.GlobalTransform * fox.GetAabb();
            Check(bounds.Size.Y is > 0.3f and < 2.0f,
                $"the Fox is scaled to human-ish height (y size {bounds.Size.Y:0.##})");
            Check(bounds.Size.Z is > 0.5f and < 3.0f,
                $"the Fox is scaled sanely along its length (z size {bounds.Size.Z:0.##})");
            Check(Mathf.Abs(bounds.Position.Y) < 0.3f,
                $"the Fox stands on the ground rather than floating or sinking (y min {bounds.Position.Y:0.##})");
        }

        Check(player.State == PlayerController3D.LocomotionState.Idle, "the player starts Idle");
        Check(animationPlayer == null || animationPlayer.CurrentAnimation == "Survey",
            $"the idle clip plays while stationary (playing '{animationPlayer?.CurrentAnimation}')");

        var cameraOffset = camera.GlobalPosition - player.GlobalPosition;
        var cameraBasis = camera.GlobalBasis;

        // Drive the real signal path rather than calling MoveTo directly.
        var target = new Vector3(6, 0, 0);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.GroundClicked, target);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Check(player.HasDestination, "GroundClicked over EventBus sets a destination");

        await WaitPhysics(10);
        Check(player.State == PlayerController3D.LocomotionState.Walking, "the player reports Walking while moving");
        Check(animationPlayer == null || animationPlayer.CurrentAnimation == "Walk",
            $"the walk clip plays while moving (playing '{animationPlayer?.CurrentAnimation}')");

        // Facing: -Z is forward, so heading towards +X means a yaw near +90 deg.
        var forward = -player.GlobalBasis.Z;
        Check(forward.X > 0.5f, $"the player rotates towards the destination (forward x {forward.X:0.##})");

        // The model's own forward is +Z, so Visual carries a 180 deg yaw. Check
        // the mesh really points down the travel direction, not backwards.
        if (fox != null)
        {
            var foxForward = fox.GlobalBasis.Z.Normalized();
            Check(foxForward.Dot(forward) > 0.5f,
                $"the Fox model faces the travel direction (dot {foxForward.Dot(forward):0.##})");
        }

        await WaitPhysics(150);
        Check(!player.HasDestination, "the player clears its destination on arrival");
        Check(player.State == PlayerController3D.LocomotionState.Idle, "the player returns to Idle");
        Check(animationPlayer == null || animationPlayer.CurrentAnimation == "Survey",
            $"the idle clip resumes after arrival (playing '{animationPlayer?.CurrentAnimation}')");
        Check(player.GlobalPosition.DistanceTo(new Vector3(target.X, player.GlobalPosition.Y, target.Z)) < 0.5f,
            $"the player stops at the destination (x {player.GlobalPosition.X:0.##}, z {player.GlobalPosition.Z:0.##})");

        // Following must not change the camera's angle or its distance.
        Check(camera.GlobalBasis.IsEqualApprox(cameraBasis), "the camera keeps its fixed rotation");
        Check(Mathf.Abs((camera.GlobalPosition - player.GlobalPosition).Length() - cameraOffset.Length()) < 0.5f,
            "the camera keeps its distance from the player");

        await CheckEnvironment(world, player, detector);
        await CheckCombat(world, player);

        world.QueueFree();
    }

    // The combat prototype, driven through the real input actions and the real
    // AbilitySystem rather than by calling the effects directly.
    private async System.Threading.Tasks.Task CheckCombat(Node world, PlayerController3D player)
    {
        GD.Print("\ncombat");

        var abilities = world.GetNodeOrNull<AbilitySystem>("Player3D/AbilitySystem");
        var playerHealth = world.GetNodeOrNull<HealthComponent>("Player3D/Health");
        var abilityBar = world.GetNodeOrNull<AbilityBar>("AbilityBar");
        var monsters = world.GetNodeOrNull<Node3D>("Monsters");

        Check(abilities != null, "the player has an AbilitySystem");
        Check(playerHealth != null, "the player has a HealthComponent");
        Check(abilityBar != null, "the AbilityBar is in the level");
        Check(world.GetNodeOrNull<HealthBar>("Player3D/HealthBar") != null, "the player has a world-space health bar");
        Check(monsters != null && monsters.GetChildCount() == 3, "three monsters are in the level");

        if (abilities == null || playerHealth == null || monsters == null)
        {
            GD.Print("  (skipping combat checks: the level is missing nodes)");
            return;
        }

        Check(playerHealth.MaxHealth == 100, "the player has 100 max HP");

        // Abilities and their cooldowns, as registered by PlayerAbilities.
        foreach (var (action, key, cooldown) in new[]
        {
            ("ability_q", "Q", 5.0f), ("ability_w", "W", 8.0f),
            ("ability_e", "E", 12.0f), ("ability_r", "R", 30.0f),
        })
        {
            var ability = abilities.Get(action);
            Check(ability != null, $"{action} is registered");
            Check(ability?.Key == key, $"{action} displays as {key}");
            Check(ability != null && Mathf.IsEqualApprox(ability.Cooldown, cooldown),
                $"{action} has a {cooldown:0}s cooldown");
        }

        Check(abilities.Get("ability_r").Cooldown > abilities.Get("ability_e").Cooldown,
            "R has the longest cooldown of the four");

        foreach (Node child in monsters.GetChildren())
        {
            var monster = child as EnemyController;
            Check(monster != null, $"{child.Name} is an EnemyController");
            Check(monster?.Health?.MaxHealth == 50, $"{child.Name} has 50 max HP");
            Check(monster?.GetNodeOrNull<HealthBar>("HealthBar") != null, $"{child.Name} has a health bar");
            Check(monster != null && monster.IsInGroup(EnemyController.EnemyGroup),
                $"{child.Name} joined the enemies group");
        }

        // Q: only the nearest monster is hit. Park the player between two of them
        // with one clearly closer.
        var near = monsters.GetChild<EnemyController>(0);
        var far = monsters.GetChild<EnemyController>(1);
        near.GlobalPosition = new Vector3(2.0f, 0.1f, 0.0f);
        far.GlobalPosition = new Vector3(20.0f, 0.1f, 0.0f);
        player.GlobalPosition = new Vector3(0.0f, 0.1f, 0.0f);
        playerHealth.Heal(playerHealth.MaxHealth);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        await PressAction("ability_q");
        Check(near.Health.CurrentHealth == 30, $"Q deals 20 to the closest monster (HP {near.Health.CurrentHealth})");
        Check(far.Health.CurrentHealth == 50, "Q leaves the distant monster untouched");
        Check(!abilities.Get("ability_q").IsReady, "Q goes on cooldown after casting");

        // Recasting on cooldown must not deal damage again.
        await PressAction("ability_q");
        Check(near.Health.CurrentHealth == 30, "Q on cooldown deals no damage");

        // W: everything inside 3m, nothing outside it.
        var second = monsters.GetChild<EnemyController>(2);
        second.GlobalPosition = new Vector3(-2.0f, 0.1f, 0.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        await PressAction("ability_w");
        Check(near.Health.CurrentHealth == 15 && second.Health.CurrentHealth == 35,
            $"W deals 15 to both nearby monsters ({near.Health.CurrentHealth}, {second.Health.CurrentHealth})");
        Check(far.Health.CurrentHealth == 50, "W does not reach a monster 20m away");

        // E: dash along the current facing, without leaving the ground.
        player.GlobalPosition = new Vector3(0.0f, 0.1f, 6.0f);
        player.Rotation = new Vector3(0.0f, 0.0f, 0.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var beforeDash = player.GlobalPosition;
        var facing = player.Facing;

        await PressAction("ability_e");
        await WaitPhysics(2);
        var travelled = player.GlobalPosition - beforeDash;
        travelled.Y = 0.0f;
        Check(travelled.Length() > 2.0f, $"E dashes the player (moved {travelled.Length():0.##}m)");
        Check(travelled.Normalized().Dot(facing) > 0.9f, "the dash follows the facing direction");
        Check(player.GlobalPosition.Y > -1.0f, "the dash does not drop the player through the map");

        // A dash aimed off the edge must land short rather than into the void.
        player.GlobalPosition = new Vector3(19.0f, 0.1f, 0.0f);
        player.Rotation = new Vector3(0.0f, -Mathf.Pi * 0.5f, 0.0f);
        abilities.Get("ability_e").ResetCooldown();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await PressAction("ability_e");
        await WaitPhysics(5);
        Check(player.GlobalPosition.Y > -1.0f,
            $"a dash towards the edge stays on the ground (y {player.GlobalPosition.Y:0.##})");

        // R: big radius, big damage, and it kills what Q and W left standing.
        player.GlobalPosition = new Vector3(0.0f, 0.1f, 0.0f);
        near.GlobalPosition = new Vector3(3.0f, 0.1f, 0.0f);
        second.GlobalPosition = new Vector3(-3.0f, 0.1f, 0.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        await PressAction("ability_r");
        Check(!near.Health.IsAlive, "R kills the monster that was down to 15 HP");
        Check(second.Health.CurrentHealth == 0 || !second.Health.IsAlive, "R kills the monster at 35 HP too");
        Check(far.Health.CurrentHealth == 50, "R still does not reach the distant monster");
        // Cooldown has already ticked for the frames PressAction waited out, so
        // check it started near the full 30s rather than exactly at it.
        Check(abilities.Get("ability_r").CurrentCooldown > 29.0f,
            $"R starts its 30s cooldown ({abilities.Get("ability_r").CurrentCooldown:0.##}s left)");

        // Death: out of the group first, so a follow-up cast cannot target a corpse.
        Check(!near.IsInGroup(EnemyController.EnemyGroup), "a dead monster leaves the enemies group");
        Check(near.CollisionLayer == 0 && near.CollisionMask == 0, "a dead monster stops colliding");
        Check(near.GetNodeOrNull<HealthBar>("HealthBar") is { Visible: false }, "a dead monster hides its health bar");

        // Monsters despawn shortly after dying.
        await ToSignal(GetTree().CreateTimer(1.0f), Timer.SignalName.Timeout);
        Check(!IsInstanceValid(near) || near.IsQueuedForDeletion(), "a dead monster is freed");

        // Enemy AI: the survivor closes in and starts hitting the player.
        far.GlobalPosition = new Vector3(4.0f, 0.1f, 0.0f);
        player.GlobalPosition = new Vector3(0.0f, 0.1f, 0.0f);
        player.Stop();
        playerHealth.Heal(playerHealth.MaxHealth);
        var ai = far.GetNodeOrNull<EnemyAI>("EnemyAI");
        Check(ai != null, "the monster has an EnemyAI");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var startDistance = far.GlobalPosition.DistanceTo(player.GlobalPosition);
        await WaitPhysics(30);
        Check(ai?.State is EnemyAI.AIState.Aggro or EnemyAI.AIState.Attack,
            $"the monster aggros inside 6m (state {ai?.State})");
        Check(far.GlobalPosition.DistanceTo(player.GlobalPosition) < startDistance - 0.5f,
            "the monster closes the distance");

        // Wait out one attack interval and confirm the player is being chipped.
        var healthBeforeAttack = playerHealth.CurrentHealth;
        await WaitPhysics(200);
        Check(ai?.State == EnemyAI.AIState.Attack, $"the monster stops to attack (state {ai?.State})");
        Check(playerHealth.CurrentHealth < healthBeforeAttack,
            $"the player takes damage from the monster (HP {playerHealth.CurrentHealth})");
        Check((healthBeforeAttack - playerHealth.CurrentHealth) % 5 == 0, "attacks land in 5 HP bites");

        // Out of range again: back to idle, no more damage.
        far.GlobalPosition = new Vector3(25.0f, 0.1f, 0.0f);
        await WaitPhysics(10);
        var healthAfterRetreat = playerHealth.CurrentHealth;
        await WaitPhysics(150);
        Check(ai?.State == EnemyAI.AIState.Idle, $"the monster idles when out of range (state {ai?.State})");
        Check(playerHealth.CurrentHealth == healthAfterRetreat, "an idle monster deals no damage");

        // Player death stops movement. No respawn yet, by design.
        playerHealth.Die();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Check(!player.IsAlive, "the player can die");
        player.MoveTo(new Vector3(8, 0, 8));
        Check(!player.HasDestination, "a dead player ignores movement orders");
    }

    // Fires a real InputMap action so the test exercises AbilitySystem's
    // _UnhandledInput path rather than calling the effect directly.
    // Input.ActionPress only sets the polled action state - it never dispatches
    // an InputEvent - so a parsed event is required to reach _UnhandledInput.
    private async System.Threading.Tasks.Task PressAction(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    // The training area: every clickable surface must be on the world layer, and
    // decoration must stay off it so it cannot swallow a ground click.
    private async System.Threading.Tasks.Task CheckEnvironment(
        Node world, PlayerController3D player, GroundClickDetector detector)
    {
        GD.Print("\nenvironment");

        var environment = world.GetNodeOrNull<Node3D>("Environment");
        Check(environment != null, "the Environment group exists");
        if (environment == null)
        {
            return;
        }

        foreach (var group in new[] { "Path", "Trees", "Rocks", "Bushes", "Building", "Fences" })
        {
            Check(environment.GetNodeOrNull<Node3D>(group) != null, $"Environment/{group} exists");
        }

        Check(environment.GetNodeOrNull<Node3D>("Trees").GetChildCount() is >= 5 and <= 8, "5-8 trees");
        Check(environment.GetNodeOrNull<Node3D>("Rocks").GetChildCount() is >= 4 and <= 6, "4-6 rocks");
        Check(environment.GetNodeOrNull<Node3D>("Bushes").GetChildCount() >= 3, "a few bushes");

        // Solid props share the ground's layer so the player collides with them
        // and so a click on one is treated as "not walkable ground".
        var solids = 0;
        var wrongLayer = 0;
        foreach (var body in CollectBodies(environment))
        {
            solids++;
            if (body.CollisionLayer != 1 || body.CollisionMask != 0)
            {
                wrongLayer++;
            }
        }

        Check(solids >= 14, $"the environment has solid bodies ({solids})");
        Check(wrongLayer == 0, $"every solid body is layer 1 / mask 0 ({wrongLayer} wrong)");

        // Decoration must not be solid, or the fox snags on ankle-high geometry.
        Check(CollectBodies(environment.GetNodeOrNull<Node3D>("Bushes")).Count == 0, "bushes have no collider");
        Check(CollectBodies(environment.GetNodeOrNull<Node3D>("Path")).Count == 0, "the path has no collider");

        // Ground clicks across the whole playable area, driven through the real
        // camera projection at the corners and edges of the viewport.
        if (detector != null)
        {
            var size = GetViewport().GetVisibleRect().Size;
            var hits = 0;
            var offGround = 0;
            foreach (var uv in new[]
            {
                new Vector2(0.5f, 0.5f), new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f),
                new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f), new Vector2(0.5f, 0.2f),
                new Vector2(0.5f, 0.8f),
            })
            {
                if (!detector.TryGetGroundPoint(uv * size, out var point))
                {
                    continue;
                }

                hits++;
                if (Mathf.Abs(point.Y) > 0.35f)
                {
                    offGround++;
                }
            }

            Check(hits == 7, $"rays across the viewport all hit something ({hits}/7)");
            Check(offGround == 0, $"those hits land on the ground plane ({offGround} off-plane)");
        }

        // Walk a lap of the play area: the fox must stay on top of the ground the
        // whole way, not tunnel through it or get stuck on scenery. The waypoints
        // deliberately keep to clear ground and the road - movement is still
        // straight-line, so a leg aimed through a tree would stall by design.
        var brokeThrough = false;
        foreach (var waypoint in new[]
        {
            new Vector3(8, 0, 4), new Vector3(0, 0, 12), new Vector3(-5, 0, 2),
            new Vector3(0, 0, -12), new Vector3(0, 0, 0),
        })
        {
            player.MoveTo(waypoint);
            for (var i = 0; i < 240 && player.HasDestination; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (player.GlobalPosition.Y < -1.0f)
                {
                    brokeThrough = true;
                }
            }

            Check(!player.HasDestination,
                $"the fox reaches ({waypoint.X}, {waypoint.Z}) without getting stuck");
        }

        Check(!brokeThrough, "the fox never falls through the ground");
        Check(player.IsOnFloor(), "the fox is still standing on the floor at the end of the lap");

        // The other half of the collision contract: a solid prop must actually
        // stop the fox rather than letting it slide through.
        player.GlobalPosition = new Vector3(-9, 0.1f, -3.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.MoveTo(new Vector3(-9, 0, -8));
        await WaitPhysics(90);
        Check(player.GlobalPosition.Z > -5.4f,
            $"the hut blocks the fox instead of letting it pass (z {player.GlobalPosition.Z:0.##})");
        player.Stop();
        player.GlobalPosition = new Vector3(0, 0.1f, 0);
    }

    private static System.Collections.Generic.List<PhysicsBody3D> CollectBodies(Node root)
    {
        var found = new System.Collections.Generic.List<PhysicsBody3D>();
        if (root == null)
        {
            return found;
        }

        if (root is PhysicsBody3D body)
        {
            found.Add(body);
        }

        foreach (var child in root.GetChildren())
        {
            found.AddRange(CollectBodies(child));
        }

        return found;
    }

    private async System.Threading.Tasks.Task WaitPhysics(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
