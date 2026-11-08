using Godot;

public partial class DialogueBox : CanvasLayer
{
    [Export] public NodePath PanelPath { get; set; }
    [Export] public NodePath NameLabelPath { get; set; }
    [Export] public NodePath TextLabelPath { get; set; }

    private Control _panel;
    private Label _nameLabel;
    private Label _textLabel;

    public override void _Ready()
    {
        _panel = GetNode<Control>(PanelPath);
        _nameLabel = GetNode<Label>(NameLabelPath);
        _textLabel = GetNode<Label>(TextLabelPath);
        _panel.Visible = false;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.DialogueStarted += OnDialogueStarted;
        }
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.DialogueStarted -= OnDialogueStarted;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_panel.Visible && @event.IsActionPressed("interact"))
        {
            CloseDialogue();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnDialogueStarted(string speakerName, string text)
    {
        _nameLabel.Text = speakerName;
        _textLabel.Text = text;
        _panel.Visible = true;
        GameManager.Instance?.PauseGame(true);
    }

    private void CloseDialogue()
    {
        _panel.Visible = false;
        GameManager.Instance?.PauseGame(false);
        EventBus.Instance?.EmitSignal(EventBus.SignalName.DialogueEnded);
    }
}
