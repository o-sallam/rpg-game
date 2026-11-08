using Godot;

// Drives the character model's AnimationPlayer from the controller's
// locomotion state. Kept as its own node so the model and its clip names can
// be swapped without touching movement code: point the exports at a different
// AnimationPlayer and clip names and nothing else changes.
public partial class PlayerAnimator : Node
{
    [Export] public NodePath ControllerPath { get; set; }
    [Export] public NodePath AnimationPlayerPath { get; set; }

    // Clip names as they appear in the imported GLB. Fox.glb ships
    // Survey / Walk / Run; Run is unused for now.
    [Export] public string IdleAnimation { get; set; } = "Survey";
    [Export] public string WalkAnimation { get; set; } = "Walk";

    [Export] public float BlendTime { get; set; } = 0.15f;

    private PlayerController3D _controller;
    private AnimationPlayer _animationPlayer;

    public override void _Ready()
    {
        _controller = GetNodeOrNull<PlayerController3D>(ControllerPath);
        _animationPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);

        if (_animationPlayer == null)
        {
            GD.PushWarning($"{Name}: no AnimationPlayer at '{AnimationPlayerPath}'.");
            return;
        }

        if (_controller != null)
        {
            _controller.LocomotionStateChanged += OnLocomotionStateChanged;
            Apply(_controller.State);
        }
        else
        {
            GD.PushWarning($"{Name}: no PlayerController3D at '{ControllerPath}'.");
            Apply(PlayerController3D.LocomotionState.Idle);
        }
    }

    public override void _ExitTree()
    {
        if (_controller != null)
        {
            _controller.LocomotionStateChanged -= OnLocomotionStateChanged;
        }
    }

    private void OnLocomotionStateChanged(int state)
    {
        Apply((PlayerController3D.LocomotionState)state);
    }

    private void Apply(PlayerController3D.LocomotionState state)
    {
        if (_animationPlayer == null)
        {
            return;
        }

        var clip = state == PlayerController3D.LocomotionState.Walking ? WalkAnimation : IdleAnimation;

        if (!_animationPlayer.HasAnimation(clip))
        {
            GD.PushWarning($"{Name}: animation '{clip}' is missing from {_animationPlayer.Name}.");
            return;
        }

        if (_animationPlayer.CurrentAnimation != clip)
        {
            _animationPlayer.Play(clip, BlendTime);
        }
    }
}
