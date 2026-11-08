using Godot;

// World-space health bar, built from two flat quads plus an optional Label3D.
// Used by both the player and the monsters - it only knows about a
// HealthComponent, not about what owns it.
//
// The bar copies the camera's orientation each frame instead of using billboard
// materials. That keeps the bar's local X axis aligned with screen-right, so the
// fill can be offset and centred with plain node positions, and it cancels the
// owner's yaw - the bar never skews or spins as the fox turns.
public partial class HealthBar : Node3D
{
    [Export] public NodePath HealthPath { get; set; }

    // Scaled horizontally to show the remaining fraction.
    [Export] public NodePath FillPath { get; set; }

    // Optional "85 / 100" readout.
    [Export] public NodePath LabelPath { get; set; }

    // Unscaled width of the fill quad, needed to keep it left-aligned as it shrinks.
    [Export] public float BarWidth { get; set; } = 1.0f;

    [Export] public bool ShowLabel { get; set; } = true;
    [Export] public bool HideWhenDead { get; set; } = true;

    // Applied as an unshaded material override on the fill quad, so one bar
    // scene can serve the player (green) and the monsters (red).
    [Export] public Color FillColor { get; set; } = new Color(0.36f, 0.78f, 0.38f);

    private HealthComponent _health;
    private Node3D _fill;
    private Label3D _label;
    private float _fillScaleY = 1.0f;
    private float _fillScaleZ = 1.0f;

    public override void _Ready()
    {
        _health = GetNodeOrNull<HealthComponent>(HealthPath);
        _fill = GetNodeOrNull<Node3D>(FillPath);
        _label = GetNodeOrNull<Label3D>(LabelPath);

        if (_fill != null)
        {
            // Preserve whatever non-horizontal scale the scene authored.
            _fillScaleY = _fill.Scale.Y;
            _fillScaleZ = _fill.Scale.Z;

            if (_fill is GeometryInstance3D fillGeometry)
            {
                fillGeometry.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = FillColor,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    NoDepthTest = true,
                };
            }
        }

        if (_label != null)
        {
            _label.Visible = ShowLabel;
        }

        if (_health == null)
        {
            GD.PushWarning($"{Name}: no HealthComponent at '{HealthPath}'.");
            return;
        }

        _health.HealthChanged += OnHealthChanged;
        _health.Died += OnDied;

        // Draw the starting value immediately rather than waiting for damage.
        Redraw(_health.CurrentHealth, _health.MaxHealth);
        FaceCamera();
    }

    public override void _ExitTree()
    {
        if (_health != null)
        {
            _health.HealthChanged -= OnHealthChanged;
            _health.Died -= OnDied;
        }
    }

    public override void _Process(double delta)
    {
        FaceCamera();
    }

    private void FaceCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera != null)
        {
            GlobalBasis = camera.GlobalBasis;
        }
    }

    private void OnHealthChanged(int currentHealth, int maxHealth) => Redraw(currentHealth, maxHealth);

    private void OnDied()
    {
        if (HideWhenDead)
        {
            Visible = false;
        }
    }

    private void Redraw(int currentHealth, int maxHealth)
    {
        var fraction = maxHealth <= 0 ? 0.0f : Mathf.Clamp((float)currentHealth / maxHealth, 0.0f, 1.0f);

        if (_fill != null)
        {
            _fill.Scale = new Vector3(Mathf.Max(fraction, 0.0001f), _fillScaleY, _fillScaleZ);
            // Quads scale about their centre, so shift left to keep the bar
            // draining towards one end instead of shrinking inwards.
            _fill.Position = new Vector3(-BarWidth * (1.0f - fraction) * 0.5f, _fill.Position.Y, _fill.Position.Z);
        }

        if (_label != null && ShowLabel)
        {
            _label.Text = $"{currentHealth} / {maxHealth}";
        }
    }
}
