using Godot;
using System.Collections.Generic;

// Bottom-centre ability bar. Finds the AbilitySystem, pairs each AbilitySlot
// child to an ability by action name, and refreshes the slots every frame so
// cooldowns count down on screen.
public partial class AbilityBar : CanvasLayer
{
    [Export] public NodePath AbilitySystemPath { get; set; }

    // Where the slots live; searched recursively for AbilitySlot nodes.
    [Export] public NodePath SlotContainerPath { get; set; }

    private AbilitySystem _abilities;
    private readonly List<AbilitySlot> _slots = new();

    public override void _Ready()
    {
        _abilities = GetNodeOrNull<AbilitySystem>(AbilitySystemPath);

        var container = GetNodeOrNull<Node>(SlotContainerPath) ?? this;
        CollectSlots(container);

        if (_abilities == null)
        {
            GD.PushWarning($"{Name}: no AbilitySystem at '{AbilitySystemPath}'.");
            return;
        }

        // The player registers its abilities in _Ready, which may run after this
        // node's. Defer the binding by one frame so the list is populated.
        CallDeferred(MethodName.BindSlots);
    }

    private void CollectSlots(Node root)
    {
        if (root is AbilitySlot slot)
        {
            _slots.Add(slot);
        }

        foreach (var child in root.GetChildren())
        {
            CollectSlots(child);
        }
    }

    private void BindSlots()
    {
        foreach (var slot in _slots)
        {
            var ability = _abilities.Get(slot.Action);

            if (ability == null)
            {
                GD.PushWarning($"{Name}: no ability registered for '{slot.Action}'.");
                continue;
            }

            slot.Bind(ability);
        }
    }

    public override void _Process(double delta)
    {
        foreach (var slot in _slots)
        {
            slot.Refresh();
        }
    }
}
