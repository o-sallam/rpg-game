using Godot;

// World object that hands an InventoryItem to whichever PlayerController
// walks into it, then removes itself.
public partial class ItemPickup : Area2D
{
    [Export] public InventoryItem Item { get; set; }
    [Export] public int Quantity { get; set; } = 1;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not PlayerController player || Item == null)
        {
            return;
        }

        var inventory = player.GetNodeOrNull<Inventory>("Inventory");
        if (inventory != null && inventory.AddItem(Item, Quantity))
        {
            QueueFree();
        }
    }
}
