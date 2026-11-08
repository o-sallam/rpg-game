using Godot;

// Three-state monster brain: stand still, walk at the player, hit the player.
// Straight-line steering, matching the player's own movement - no navigation
// mesh yet, deliberately.
public partial class EnemyAI : Node
{
    public enum AIState
    {
        Idle,
        Aggro,
        Attack,
        Dead,
    }

    [Export] public NodePath ControllerPath { get; set; }
    [Export] public float AggroRange { get; set; } = 6.0f;
    [Export] public float AttackRange { get; set; } = 1.5f;
    [Export] public float MoveSpeed { get; set; } = 2.6f;
    [Export] public float RotationSpeed { get; set; } = 8.0f;
    [Export] public int AttackDamage { get; set; } = 5;
    [Export] public float AttackInterval { get; set; } = 2.0f;

    public AIState State { get; private set; } = AIState.Idle;

    private EnemyController _enemy;
    private Node3D _player;
    private HealthComponent _playerHealth;
    private float _attackTimer;

    public override void _Ready()
    {
        _enemy = GetNodeOrNull<EnemyController>(ControllerPath) ?? GetParent<EnemyController>();

        if (_enemy == null)
        {
            GD.PushWarning($"{Name}: no EnemyController at '{ControllerPath}'.");
            SetPhysicsProcess(false);
            return;
        }

        if (_enemy.Health != null)
        {
            _enemy.Health.Died += OnDied;
        }
    }

    public override void _ExitTree()
    {
        if (_enemy?.Health != null)
        {
            _enemy.Health.Died -= OnDied;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (State == AIState.Dead || _enemy == null)
        {
            return;
        }

        var dt = (float)delta;
        var player = ResolvePlayer();

        if (player == null)
        {
            _enemy.SetPlanarVelocity(Vector3.Zero, dt);
            return;
        }

        var toPlayer = player.GlobalPosition - _enemy.GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();

        if (distance <= AttackRange)
        {
            State = AIState.Attack;
            _enemy.SetPlanarVelocity(Vector3.Zero, dt);
            _enemy.FaceDirection(toPlayer, RotationSpeed, dt);
            TickAttack(dt);
        }
        else if (distance <= AggroRange)
        {
            State = AIState.Aggro;
            _enemy.SetPlanarVelocity(toPlayer.Normalized() * MoveSpeed, dt);
            _enemy.FaceDirection(toPlayer, RotationSpeed, dt);
            // Wind up while closing, so the first hit lands on contact.
            _attackTimer = 0.0f;
        }
        else
        {
            State = AIState.Idle;
            _enemy.SetPlanarVelocity(Vector3.Zero, dt);
            _attackTimer = 0.0f;
        }
    }

    private void TickAttack(float delta)
    {
        _attackTimer -= delta;
        if (_attackTimer > 0.0f)
        {
            return;
        }

        _attackTimer = AttackInterval;

        if (_playerHealth is { IsAlive: true })
        {
            _playerHealth.TakeDamage(AttackDamage);
        }
    }

    // Resolved lazily and cached: the player may be added to the tree after the
    // monsters, and it can be freed while the level is still running.
    private Node3D ResolvePlayer()
    {
        if (_player != null && IsInstanceValid(_player))
        {
            return _player;
        }

        _player = null;
        _playerHealth = null;

        foreach (var node in GetTree().GetNodesInGroup(PlayerAbilities.PlayerGroup))
        {
            if (node is not Node3D body)
            {
                continue;
            }

            _player = body;
            _playerHealth = body.GetNodeOrNull<HealthComponent>("Health");
            break;
        }

        return _player;
    }

    private void OnDied()
    {
        State = AIState.Dead;
        SetPhysicsProcess(false);
    }
}
