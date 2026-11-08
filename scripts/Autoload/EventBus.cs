using Godot;

// Global signal hub. Autoloaded as a singleton so any node can emit or
// listen for game-wide events without holding direct references to
// each other (player, UI, dialogue, inventory, etc).
public partial class EventBus : Node
{
	public static EventBus Instance { get; private set; }

	[Signal]
	public delegate void PlayerHealthChangedEventHandler(int currentHealth, int maxHealth);

	[Signal]
	public delegate void PlayerLeveledUpEventHandler(int newLevel);

	[Signal]
	public delegate void DialogueStartedEventHandler(string speakerName, string text);

	[Signal]
	public delegate void DialogueEndedEventHandler();

	[Signal]
	public delegate void InventoryChangedEventHandler();

	// Emitted by GroundClickDetector with a world-space point on the ground.
	[Signal]
	public delegate void GroundClickedEventHandler(Vector3 worldPosition);

	public override void _EnterTree()
	{
		Instance = this;
	}
}
