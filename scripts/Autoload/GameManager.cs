using Godot;

// Small autoloaded singleton for scene transitions and pause state.
// Kept intentionally thin: game-wide data belongs in EventBus/PlayerStats,
// this just coordinates the SceneTree.
public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	public override void _EnterTree()
	{
		Instance = this;
	}

	public void ChangeScene(string scenePath)
	{
		GetTree().ChangeSceneToFile(scenePath);
	}

	public void PauseGame(bool paused)
	{
		GetTree().Paused = paused;
	}

	public void QuitGame()
	{
		GetTree().Quit();
	}
}
