using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody2D
{
    [Export] public PlayerStats Stats { get; set; }
    [Export] public NodePath InteractionAreaPath { get; set; }

    private readonly List<Node> _interactablesInRange = new();

    public override void _Ready()
    {
        Stats ??= new PlayerStats();

        if (InteractionAreaPath != null && !InteractionAreaPath.IsEmpty)
        {
            var interactionArea = GetNode<Area2D>(InteractionAreaPath);
            interactionArea.AreaEntered += OnInteractionAreaEntered;
            interactionArea.AreaExited += OnInteractionAreaExited;
        }

        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, Stats.CurrentHealth, Stats.MaxHealth);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = inputDirection * Stats.MoveSpeed;
        MoveAndSlide();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("interact"))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        foreach (var node in _interactablesInRange)
        {
            if (node is IInteractable interactable)
            {
                interactable.Interact(this);
                return;
            }
        }
    }

    private void OnInteractionAreaEntered(Area2D area)
    {
        if (area.GetParent() is IInteractable && !_interactablesInRange.Contains(area.GetParent()))
        {
            _interactablesInRange.Add(area.GetParent());
        }
    }

    private void OnInteractionAreaExited(Area2D area)
    {
        _interactablesInRange.Remove(area.GetParent());
    }
}
