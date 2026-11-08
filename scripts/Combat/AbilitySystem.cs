using Godot;
using System.Collections.Generic;

// Owns a list of abilities, ticks their cooldowns, and turns InputMap actions
// into casts. It knows nothing about what any ability actually does - that is
// supplied by whoever registers them (PlayerAbilities).
public partial class AbilitySystem : Node
{
    [Signal]
    public delegate void AbilityUsedEventHandler(string action);

    private readonly List<Ability> _abilities = new();

    public IReadOnlyList<Ability> Abilities => _abilities;

    public void Register(Ability ability)
    {
        if (ability == null)
        {
            return;
        }

        if (!InputMap.HasAction(ability.Action))
        {
            GD.PushWarning($"{Name}: no InputMap action '{ability.Action}' for {ability.DisplayName}.");
        }

        _abilities.Add(ability);
    }

    public Ability Get(string action) => _abilities.Find(a => a.Action == action);

    public override void _Process(double delta)
    {
        foreach (var ability in _abilities)
        {
            ability.Tick((float)delta);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        foreach (var ability in _abilities)
        {
            if (!@event.IsActionPressed(ability.Action))
            {
                continue;
            }

            if (ability.Use())
            {
                EmitSignal(SignalName.AbilityUsed, ability.Action);
            }

            GetViewport().SetInputAsHandled();
            return;
        }
    }
}
