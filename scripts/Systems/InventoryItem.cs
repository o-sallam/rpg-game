using Godot;

[GlobalClass]
public partial class InventoryItem : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "Item";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export] public bool Stackable { get; set; } = true;
    [Export] public int MaxStack { get; set; } = 99;
}
