using Godot;

// A monster body: health, death handling, and the visual bits that go with it.
// The AI that decides where it walks lives in EnemyAI, so this stays the piece
// that abilities and health bars talk to.
public partial class EnemyController : CharacterBody3D
{
    // Abilities find their targets through this group rather than through node
    // paths, so monsters can be added to a level without wiring anything up.
    public const string EnemyGroup = "enemies";

    [Export] public NodePath HealthPath { get; set; }
    [Export] public NodePath VisualPath { get; set; }
    [Export] public float Gravity { get; set; } = 20.0f;

    // Left on the corpse long enough to see the last hit land, then freed.
    [Export] public float DespawnDelay { get; set; } = 0.6f;

    public HealthComponent Health { get; private set; }
    public bool IsAlive => Health == null || Health.IsAlive;

    private Node3D _visual;

    public override void _Ready()
    {
        AddToGroup(EnemyGroup);

        Health = GetNodeOrNull<HealthComponent>(HealthPath);
        _visual = GetNodeOrNull<Node3D>(VisualPath);

        if (Health != null)
        {
            Health.Died += OnDied;
        }
        else
        {
            GD.PushWarning($"{Name}: no HealthComponent at '{HealthPath}'.");
        }
    }

    public override void _ExitTree()
    {
        if (Health != null)
        {
            Health.Died -= OnDied;
        }
    }

    public void TakeDamage(int amount) => Health?.TakeDamage(amount);

    public void Die() => Health?.Die();

    // Monsters do not steer themselves; EnemyAI sets this each frame.
    public void SetPlanarVelocity(Vector3 planar, float delta)
    {
        var velocity = Velocity;
        velocity.X = planar.X;
        velocity.Z = planar.Z;
        velocity.Y = IsOnFloor() ? 0.0f : velocity.Y - Gravity * delta;
        Velocity = velocity;
        MoveAndSlide();
    }

    public void FaceDirection(Vector3 direction, float rotationSpeed, float delta)
    {
        direction.Y = 0.0f;
        if (direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        var desiredYaw = Mathf.Atan2(-direction.X, -direction.Z);
        var weight = 1.0f - Mathf.Exp(-rotationSpeed * delta);
        Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(Rotation.Y, desiredYaw, weight), Rotation.Z);
    }

    private async void OnDied()
    {
        // Leave the group first: an ability firing this same frame must not pick
        // a corpse as its closest target.
        RemoveFromGroup(EnemyGroup);

        // Stop colliding immediately so the player can walk over the body.
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;
        SetPhysicsProcess(false);

        if (_visual != null)
        {
            _visual.Visible = false;
        }

        if (DespawnDelay > 0.0f)
        {
            await ToSignal(GetTree().CreateTimer(DespawnDelay), Timer.SignalName.Timeout);
        }

        if (IsInstanceValid(this))
        {
            QueueFree();
        }
    }
}
