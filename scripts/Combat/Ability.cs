using System;

// One castable ability: an InputMap action, a cooldown, and what it does.
//
// A plain C# class rather than a Node or Resource - it never enters the scene
// tree, it is created in code by PlayerAbilities, and AbilitySystem owns the
// list. The effect is a delegate so adding an ability means writing one method,
// not a new subclass.
public class Ability
{
    public Ability(string action, string key, string displayName, float cooldown, Action effect)
    {
        Action = action;
        Key = key;
        DisplayName = displayName;
        Cooldown = cooldown;
        _effect = effect;
    }

    // The InputMap action name, e.g. "ability_q".
    public string Action { get; }

    // What the UI prints in the slot, e.g. "Q".
    public string Key { get; }

    public string DisplayName { get; }
    public float Cooldown { get; }
    public float CurrentCooldown { get; private set; }

    private readonly Action _effect;

    public bool IsReady => CurrentCooldown <= 0.0f;

    public bool CanUse() => IsReady;

    public bool Use()
    {
        if (!CanUse())
        {
            return false;
        }

        _effect?.Invoke();
        CurrentCooldown = Cooldown;
        return true;
    }

    public void Tick(float delta)
    {
        if (CurrentCooldown > 0.0f)
        {
            CurrentCooldown = Math.Max(0.0f, CurrentCooldown - delta);
        }
    }

    public void ResetCooldown()
    {
        CurrentCooldown = 0.0f;
    }
}
