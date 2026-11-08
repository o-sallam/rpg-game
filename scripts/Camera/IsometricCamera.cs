using Godot;

// Fixed-angle orthographic camera for the 3D prototype.
//
// It never rotates and never zooms - it only keeps a constant offset from its
// target, which is what produces the classic isometric RPG framing. Every
// parameter is exported so the framing can be retuned in the Inspector
// without editing this file.
public partial class IsometricCamera : Camera3D
{
    [Export] public NodePath TargetPath { get; set; }
    [Export] public float YawDegrees { get; set; } = 45.0f;
    // Negative pitch looks down at the scene.
    [Export] public float PitchDegrees { get; set; } = -45.0f;
    [Export] public float Distance { get; set; } = 20.0f;
    [Export] public float OrthographicSize { get; set; } = 12.0f;
    [Export] public Vector3 TargetOffset { get; set; } = new Vector3(0.0f, 1.0f, 0.0f);
    // 0 pins the camera to the target; higher values ease it into place.
    [Export] public float FollowSmoothing { get; set; } = 10.0f;

    private Node3D _target;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = OrthographicSize;

        if (TargetPath != null && !TargetPath.IsEmpty)
        {
            _target = GetNodeOrNull<Node3D>(TargetPath);
        }

        ApplyOrientation();
        SnapToTarget();
    }

    public override void _Process(double delta)
    {
        if (_target == null)
        {
            return;
        }

        var desired = DesiredPosition();
        GlobalPosition = FollowSmoothing <= 0.0f
            ? desired
            : GlobalPosition.Lerp(desired, 1.0f - Mathf.Exp(-FollowSmoothing * (float)delta));
    }

    public void SetTarget(Node3D target)
    {
        _target = target;
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (_target != null)
        {
            GlobalPosition = DesiredPosition();
        }
    }

    // The camera looks along -Z, so backing off along +Z keeps the focus point
    // centred at the configured yaw/pitch.
    private Vector3 DesiredPosition() =>
        _target.GlobalPosition + TargetOffset + GlobalBasis.Z * Distance;

    private void ApplyOrientation()
    {
        Rotation = new Vector3(Mathf.DegToRad(PitchDegrees), Mathf.DegToRad(YawDegrees), 0.0f);
    }
}
