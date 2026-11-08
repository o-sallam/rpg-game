using Godot;

public partial class HUD : CanvasLayer
{
    [Export] public NodePath HealthBarPath { get; set; }
    [Export] public NodePath LevelLabelPath { get; set; }

    private ProgressBar _healthBar;
    private Label _levelLabel;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>(HealthBarPath);
        _levelLabel = GetNode<Label>(LevelLabelPath);

        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
            EventBus.Instance.PlayerLeveledUp += OnPlayerLeveledUp;
        }
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
            EventBus.Instance.PlayerLeveledUp -= OnPlayerLeveledUp;
        }
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        _healthBar.MaxValue = max;
        _healthBar.Value = current;
    }

    private void OnPlayerLeveledUp(int newLevel)
    {
        _levelLabel.Text = $"Lv. {newLevel}";
    }
}
