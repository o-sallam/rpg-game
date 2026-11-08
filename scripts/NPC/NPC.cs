using Godot;

public partial class NPC : StaticBody2D, IInteractable
{
    [Export] public string NpcName { get; set; } = "Villager";
    [Export(PropertyHint.MultilineText)] public string DialogueText { get; set; } = "Hello, traveler!";

    public void Interact(Node interactor)
    {
        EventBus.Instance?.EmitSignal(EventBus.SignalName.DialogueStarted, NpcName, DialogueText);
    }

    public string GetInteractionPrompt() => $"Talk to {NpcName}";
}
