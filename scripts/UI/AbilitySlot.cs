using Godot;

// One square in the ability bar: key letter, placeholder icon, and a cooldown
// overlay. It renders whatever Ability it is bound to and holds no game logic.
public partial class AbilitySlot : Control
{
    // Which ability this slot displays, matched by InputMap action name.
    [Export] public string Action { get; set; } = "ability_q";

    [Export] public NodePath IconPath { get; set; }
    [Export] public NodePath KeyLabelPath { get; set; }
    [Export] public NodePath CooldownLabelPath { get; set; }

    // Dark overlay drawn over the icon while the ability is unavailable.
    [Export] public NodePath CooldownOverlayPath { get; set; }

    [Export] public Color ReadyColor { get; set; } = new Color(0.28f, 0.42f, 0.62f, 1.0f);
    [Export] public Color CooldownColor { get; set; } = new Color(0.16f, 0.18f, 0.22f, 1.0f);

    private ColorRect _icon;
    private Label _keyLabel;
    private Label _cooldownLabel;
    private Control _cooldownOverlay;
    private Ability _ability;

    public override void _Ready()
    {
        _icon = GetNodeOrNull<ColorRect>(IconPath);
        _keyLabel = GetNodeOrNull<Label>(KeyLabelPath);
        _cooldownLabel = GetNodeOrNull<Label>(CooldownLabelPath);
        _cooldownOverlay = GetNodeOrNull<Control>(CooldownOverlayPath);
    }

    public void Bind(Ability ability)
    {
        _ability = ability;

        if (_keyLabel != null)
        {
            _keyLabel.Text = ability?.Key ?? "?";
        }

        if (ability != null)
        {
            TooltipText = $"{ability.Key} - {ability.DisplayName} ({ability.Cooldown:0}s)";
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_ability == null)
        {
            return;
        }

        var ready = _ability.IsReady;

        if (_icon != null)
        {
            _icon.Color = ready ? ReadyColor : CooldownColor;
        }

        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.Visible = !ready;
        }

        if (_cooldownLabel != null)
        {
            // Ceiling, so a cooldown never reads 0 while it is still running.
            _cooldownLabel.Text = ready ? "0" : $"{Mathf.CeilToInt(_ability.CurrentCooldown)}";
            _cooldownLabel.Modulate = ready
                ? new Color(1.0f, 1.0f, 1.0f, 0.55f)
                : new Color(1.0f, 0.92f, 0.55f, 1.0f);
        }
    }
}
