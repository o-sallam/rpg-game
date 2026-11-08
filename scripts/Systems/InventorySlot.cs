// Plain data holder, not a Godot object: it never lives in the scene
// tree, so it doesn't need to derive from Node/Resource.
public class InventorySlot
{
    public InventoryItem Item;
    public int Quantity;
}
