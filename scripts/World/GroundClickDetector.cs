using Godot;

// Turns a "move_to_click" press into a world-space point on the ground and
// announces it on EventBus. It is kept separate from the player so anything
// else (a move marker, a companion, a different input device) can react to the
// same click without the input code knowing about them.
public partial class GroundClickDetector : Node3D
{
    [Export] public NodePath CameraPath { get; set; }
    [Export] public float RayLength { get; set; } = 1000.0f;

    // Ground and static geometry sit on physics layer 1 ("world") and the
    // player on layer 2, so the ray cannot land on the player itself.
    [Export(PropertyHint.Layers3DPhysics)] public uint GroundMask { get; set; } = 1;

    private Camera3D _camera;

    public override void _Ready()
    {
        _camera = CameraPath != null && !CameraPath.IsEmpty
            ? GetNodeOrNull<Camera3D>(CameraPath)
            : GetViewport().GetCamera3D();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("move_to_click"))
        {
            return;
        }

        if (TryGetGroundPoint(GetViewport().GetMousePosition(), out var point))
        {
            EventBus.Instance?.EmitSignal(EventBus.SignalName.GroundClicked, point);
        }

        GetViewport().SetInputAsHandled();
    }

    public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.Zero;
        _camera ??= GetViewport().GetCamera3D();

        if (_camera == null)
        {
            return false;
        }

        var from = _camera.ProjectRayOrigin(screenPosition);
        var to = from + _camera.ProjectRayNormal(screenPosition) * RayLength;
        var query = PhysicsRayQueryParameters3D.Create(from, to, GroundMask);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (hit.Count == 0)
        {
            return false;
        }

        point = (Vector3)hit["position"];
        return true;
    }
}
