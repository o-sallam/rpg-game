using Godot;

// Click-to-move controller for the 3D prototype.
//
// Steering is deliberately split from destination-picking: MoveTo() records
// where we want to end up, GetNextPathPosition() answers "which point do I
// steer at this frame". Today those are the same point (straight-line
// movement). Dropping in a NavigationAgent3D later means overriding just
// those two members - the velocity, rotation and state code below is unaffected.
public partial class PlayerController3D : CharacterBody3D
{
    public enum LocomotionState
    {
        Idle,
        Walking,
    }

    // Emitted only on transitions, so listeners (PlayerAnimator) do not have to
    // poll. Carries an int because that is what Godot marshals for signals;
    // cast it back to LocomotionState.
    [Signal]
    public delegate void LocomotionStateChangedEventHandler(int state);

    [Export] public float MoveSpeed { get; set; } = 5.0f;
    [Export] public float RotationSpeed { get; set; } = 10.0f;
    [Export] public float ArrivalDistance { get; set; } = 0.15f;
    [Export] public float Gravity { get; set; } = 20.0f;

    // Optional: when the player's health hits 0 the controller stops steering.
    // There is no respawn yet, by design.
    [Export] public NodePath HealthPath { get; set; }

    public LocomotionState State { get; private set; } = LocomotionState.Idle;
    public bool HasDestination { get; private set; }
    public Vector3 Destination => _destination;
    public bool IsAlive => _health == null || _health.IsAlive;

    private Vector3 _destination;
    private HealthComponent _health;

    public override void _Ready()
    {
        _destination = GlobalPosition;
        _health = GetNodeOrNull<HealthComponent>(HealthPath);

        if (_health != null)
        {
            _health.Died += OnDied;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.GroundClicked += OnGroundClicked;
        }
    }

    // The autoload outlives this scene, so the subscription has to go with it.
    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.GroundClicked -= OnGroundClicked;
        }

        if (_health != null)
        {
            _health.Died -= OnDied;
        }
    }

    public virtual void MoveTo(Vector3 target)
    {
        if (!IsAlive)
        {
            return;
        }

        _destination = target;
        HasDestination = true;
    }

    public void Stop()
    {
        _destination = GlobalPosition;
        HasDestination = false;
    }

    // Override this (and MoveTo) to route movement through a NavigationAgent3D.
    protected virtual Vector3 GetNextPathPosition() => _destination;

    // A node's forward is -Z. Flattened, because abilities work on the ground plane.
    public Vector3 Facing
    {
        get
        {
            var forward = -GlobalBasis.Z;
            forward.Y = 0.0f;
            return forward.LengthSquared() < 0.0001f ? Vector3.Forward : forward.Normalized();
        }
    }

    // Sweeps the body forward instead of teleporting it, so walls and props stop
    // the dash. Also refuses to leave the ground: without that, dashing near the
    // edge of the map drops the player into the void. Returns the distance
    // actually travelled.
    public float Dash(float distance, float groundProbeHeight = 1.5f)
    {
        if (distance <= 0.0f || !IsAlive)
        {
            return 0.0f;
        }

        // A dash overrides wherever the player was walking to.
        Stop();

        var direction = Facing;
        var allowed = distance;

        // Walk the candidate distance back until the landing spot has floor
        // under it, so a dash towards the edge lands short rather than off.
        while (allowed > 0.0f && !HasGroundAt(GlobalPosition + direction * allowed, groundProbeHeight))
        {
            allowed -= 0.5f;
        }

        if (allowed <= 0.0f)
        {
            return 0.0f;
        }

        var before = GlobalPosition;
        MoveAndCollide(direction * allowed);
        return GlobalPosition.DistanceTo(before);
    }

    private bool HasGroundAt(Vector3 position, float probeHeight)
    {
        var from = position + Vector3.Up * probeHeight;
        var to = position + Vector3.Down * probeHeight;
        var query = PhysicsRayQueryParameters3D.Create(from, to, CollisionMask);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var desired = Vector3.Zero;

        if (HasDestination)
        {
            var toTarget = GetNextPathPosition() - GlobalPosition;
            toTarget.Y = 0.0f;

            if (toTarget.Length() <= ArrivalDistance)
            {
                Stop();
            }
            else
            {
                desired = toTarget.Normalized() * MoveSpeed;
            }
        }

        var velocity = Velocity;
        velocity.X = desired.X;
        velocity.Z = desired.Z;
        velocity.Y = IsOnFloor() ? 0.0f : velocity.Y - Gravity * dt;
        Velocity = velocity;
        MoveAndSlide();

        var planar = new Vector3(Velocity.X, 0.0f, Velocity.Z);
        SetState(planar.LengthSquared() > 0.01f ? LocomotionState.Walking : LocomotionState.Idle);

        if (State == LocomotionState.Walking)
        {
            FaceDirection(planar, dt);
        }
    }

    private void SetState(LocomotionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        EmitSignal(SignalName.LocomotionStateChanged, (int)state);
    }

    private void FaceDirection(Vector3 direction, float dt)
    {
        // A node's forward in Godot is -Z, hence the negated components.
        var desiredYaw = Mathf.Atan2(-direction.X, -direction.Z);
        var weight = 1.0f - Mathf.Exp(-RotationSpeed * dt);
        Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(Rotation.Y, desiredYaw, weight), Rotation.Z);
    }

    private void OnGroundClicked(Vector3 worldPosition)
    {
        MoveTo(worldPosition);
    }

    private void OnDied()
    {
        Stop();
        Velocity = Vector3.Zero;
        SetState(LocomotionState.Idle);
    }
}
