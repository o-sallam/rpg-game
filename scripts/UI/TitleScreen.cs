using Godot;

public partial class TitleScreen : Control
{
    [Export] public NodePath StartButtonPath { get; set; }
    [Export] public NodePath QuitButtonPath { get; set; }
    [Export] public string GameScenePath { get; set; } = "res://scenes/World/TestLevel.tscn";

    public override void _Ready()
    {
        GetNode<Button>(StartButtonPath).Pressed += OnStartPressed;
        GetNode<Button>(QuitButtonPath).Pressed += OnQuitPressed;
    }

    private void OnStartPressed()
    {
        GameManager.Instance?.ChangeScene(GameScenePath);
    }

    private void OnQuitPressed()
    {
        GameManager.Instance?.QuitGame();
    }
}
