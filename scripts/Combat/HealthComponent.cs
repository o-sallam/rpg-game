using Godot;

// Hit points for anything that can be damaged. Lives as a child node so the
// player and the monsters share one implementation, and so a health bar can
// find it with a NodePath instead of knowing what kind of entity it belongs to.
//
// Deliberately separate from PlayerStats: that Resource drives the 2D template
// (levels, XP, defence) and is authored in the Inspector. This is runtime-only
// combat state for the 3D prototype.
public partial class HealthComponent : Node
{
    [Signal]
    public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);

    [Signal]
    public delegate void DiedEventHandler();

    [Export] public int MaxHealth { get; set; } = 100;

    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    public float Fraction => MaxHealth <= 0 ? 0.0f : (float)CurrentHealth / MaxHealth;

    // _EnterTree rather than _Ready: it runs before every _Ready in the scene,
    // so a health bar reading CurrentHealth in its own _Ready always sees a
    // populated value regardless of sibling order.
    public override void _EnterTree()
    {
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        if (CurrentHealth == 0)
        {
            EmitSignal(SignalName.Died);
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void Die()
    {
        if (!IsAlive)
        {
            return;
        }

        CurrentHealth = 0;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.Died);
    }
}
