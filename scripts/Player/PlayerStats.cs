using Godot;

// A Resource rather than plain data so it can be saved to disk, swapped
// between characters, or authored as a preset in the editor.
[GlobalClass]
public partial class PlayerStats : Resource
{
    [Export] public string CharacterName { get; set; } = "Hero";
    [Export] public int Level { get; set; } = 1;
    [Export] public int Experience { get; set; } = 0;
    [Export] public int ExperienceToNextLevel { get; set; } = 100;
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public int CurrentHealth { get; set; } = 100;
    [Export] public int Attack { get; set; } = 10;
    [Export] public int Defense { get; set; } = 5;
    [Export] public int MoveSpeed { get; set; } = 200;

    public bool IsAlive => CurrentHealth > 0;

    public void TakeDamage(int amount)
    {
        int reduced = Mathf.Max(1, amount - Defense);
        CurrentHealth = Mathf.Max(0, CurrentHealth - reduced);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, CurrentHealth, MaxHealth);
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, CurrentHealth, MaxHealth);
    }

    public void AddExperience(int amount)
    {
        Experience += amount;
        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        Level += 1;
        MaxHealth += 20;
        Attack += 5;
        Defense += 2;
        CurrentHealth = MaxHealth;
        ExperienceToNextLevel = Mathf.RoundToInt(ExperienceToNextLevel * 1.25f);

        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerLeveledUp, Level);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PlayerHealthChanged, CurrentHealth, MaxHealth);
    }
}
