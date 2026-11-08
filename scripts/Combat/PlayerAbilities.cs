using Godot;
using System.Collections.Generic;

// Wires the player's four abilities into the AbilitySystem and implements what
// each one does. Targets are found through the enemy group, so nothing here
// holds a reference to a specific monster.
public partial class PlayerAbilities : Node
{
    // How EnemyAI finds who to chase.
    public const string PlayerGroup = "player";

    [Export] public NodePath ControllerPath { get; set; }
    [Export] public NodePath AbilitySystemPath { get; set; }

    // A flattened sphere that expands and fades on R. Standing in for a particle
    // effect, which the prototype does not need.
    [Export] public NodePath UltimateEffectPath { get; set; }

    [Export] public int QuickStrikeDamage { get; set; } = 20;
    [Export] public float QuickStrikeRange { get; set; } = 2.5f;

    [Export] public int ShockwaveDamage { get; set; } = 15;
    [Export] public float ShockwaveRadius { get; set; } = 3.0f;

    [Export] public float DashDistance { get; set; } = 4.0f;

    [Export] public int UltimateDamage { get; set; } = 50;
    [Export] public float UltimateRadius { get; set; } = 5.0f;
    [Export] public float UltimateEffectDuration { get; set; } = 0.45f;

    private PlayerController3D _controller;
    private AbilitySystem _abilities;
    private Node3D _ultimateEffect;

    public override void _Ready()
    {
        _controller = GetNodeOrNull<PlayerController3D>(ControllerPath) ?? GetParent<PlayerController3D>();
        _abilities = GetNodeOrNull<AbilitySystem>(AbilitySystemPath);
        _ultimateEffect = GetNodeOrNull<Node3D>(UltimateEffectPath);

        if (_controller != null && !_controller.IsInGroup(PlayerGroup))
        {
            _controller.AddToGroup(PlayerGroup);
        }

        if (_ultimateEffect != null)
        {
            _ultimateEffect.Visible = false;
        }

        if (_abilities == null)
        {
            GD.PushWarning($"{Name}: no AbilitySystem at '{AbilitySystemPath}'.");
            return;
        }

        _abilities.Register(new Ability("ability_q", "Q", "Quick Strike", 5.0f, QuickStrike));
        _abilities.Register(new Ability("ability_w", "W", "Shockwave", 8.0f, Shockwave));
        _abilities.Register(new Ability("ability_e", "E", "Dash", 12.0f, Dash));
        _abilities.Register(new Ability("ability_r", "R", "Ultimate", 30.0f, Ultimate));
    }

    // Q: hit the single nearest monster in front-agnostic short range.
    private void QuickStrike()
    {
        var target = FindClosestEnemy(QuickStrikeRange);
        target?.TakeDamage(QuickStrikeDamage);
    }

    // W: everything in a small ring around the player.
    private void Shockwave() => DamageEnemiesInRadius(ShockwaveRadius, ShockwaveDamage);

    // E: lunge along the current facing.
    private void Dash() => _controller?.Dash(DashDistance);

    // R: bigger ring, much bigger damage, plus the expanding shell.
    private void Ultimate()
    {
        DamageEnemiesInRadius(UltimateRadius, UltimateDamage);
        PlayUltimateEffect();
    }

    public EnemyController FindClosestEnemy(float maxDistance)
    {
        if (_controller == null)
        {
            return null;
        }

        EnemyController closest = null;
        var closestDistance = maxDistance;

        foreach (var enemy in AliveEnemies())
        {
            var distance = PlanarDistance(enemy);
            if (distance <= closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }

        return closest;
    }

    public int DamageEnemiesInRadius(float radius, int damage)
    {
        if (_controller == null)
        {
            return 0;
        }

        var hits = 0;

        // Materialised first: TakeDamage can free a monster, which would mutate
        // the group while it is being iterated.
        foreach (var enemy in AliveEnemies())
        {
            if (PlanarDistance(enemy) > radius)
            {
                continue;
            }

            enemy.TakeDamage(damage);
            hits++;
        }

        return hits;
    }

    private List<EnemyController> AliveEnemies()
    {
        var result = new List<EnemyController>();

        foreach (var node in GetTree().GetNodesInGroup(EnemyController.EnemyGroup))
        {
            if (node is EnemyController { IsAlive: true } enemy && IsInstanceValid(enemy))
            {
                result.Add(enemy);
            }
        }

        return result;
    }

    // Horizontal only, so a monster's height never affects whether it is in range.
    private float PlanarDistance(Node3D other)
    {
        var offset = other.GlobalPosition - _controller.GlobalPosition;
        offset.Y = 0.0f;
        return offset.Length();
    }

    private void PlayUltimateEffect()
    {
        if (_ultimateEffect == null)
        {
            return;
        }

        _ultimateEffect.Scale = Vector3.One * 0.2f;
        _ultimateEffect.Visible = true;

        // Scale the shell out to the real damage radius so the flash reads as
        // the area that was actually hit.
        var tween = CreateTween();
        tween.SetParallel();
        tween.TweenProperty(_ultimateEffect, "scale", Vector3.One * UltimateRadius, UltimateEffectDuration)
            .SetEase(Tween.EaseType.Out);

        if (_ultimateEffect is GeometryInstance3D geometry)
        {
            geometry.Transparency = 0.0f;
            tween.TweenProperty(geometry, "transparency", 1.0f, UltimateEffectDuration);
        }

        tween.Chain().TweenCallback(Callable.From(() => _ultimateEffect.Visible = false));
    }
}
