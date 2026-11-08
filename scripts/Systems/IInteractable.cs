using Godot;

// Implemented by anything the player can approach and press "interact" on
// (NPCs, chests, signs, doors...).
public interface IInteractable
{
    void Interact(Node interactor);
    string GetInteractionPrompt();
}
