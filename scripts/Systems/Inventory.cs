using Godot;
using System.Collections.Generic;

// Attach as a child node of any actor that should carry items (the
// player, a chest, a merchant...).
public partial class Inventory : Node
{
    [Export] public int Capacity { get; set; } = 20;

    private readonly List<InventorySlot> _slots = new();

    public IReadOnlyList<InventorySlot> Slots => _slots;

    public bool AddItem(InventoryItem item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (item.Stackable)
        {
            var existing = _slots.Find(s => s.Item.Id == item.Id && s.Quantity < item.MaxStack);
            if (existing != null)
            {
                int amountToAdd = Mathf.Min(item.MaxStack - existing.Quantity, quantity);
                existing.Quantity += amountToAdd;
                quantity -= amountToAdd;

                if (quantity <= 0)
                {
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.InventoryChanged);
                    return true;
                }
            }
        }

        if (_slots.Count >= Capacity)
        {
            return false;
        }

        _slots.Add(new InventorySlot { Item = item, Quantity = quantity });
        EventBus.Instance?.EmitSignal(EventBus.SignalName.InventoryChanged);
        return true;
    }

    public bool RemoveItem(string itemId, int quantity = 1)
    {
        var slot = _slots.Find(s => s.Item.Id == itemId);
        if (slot == null || slot.Quantity < quantity)
        {
            return false;
        }

        slot.Quantity -= quantity;
        if (slot.Quantity <= 0)
        {
            _slots.Remove(slot);
        }

        EventBus.Instance?.EmitSignal(EventBus.SignalName.InventoryChanged);
        return true;
    }
}
