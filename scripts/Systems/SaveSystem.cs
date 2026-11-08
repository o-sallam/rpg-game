using Godot;
using System.Text.Json;

// Minimal JSON save/load for PlayerStats. Call SaveSystem.Save(stats) and
// SaveSystem.Load() from wherever you want a save point (menu, checkpoint).
public static class SaveSystem
{
    private const string SavePath = "user://savegame.json";

    public static void Save(PlayerStats stats)
    {
        var data = new SaveData
        {
            CharacterName = stats.CharacterName,
            Level = stats.Level,
            Experience = stats.Experience,
            ExperienceToNextLevel = stats.ExperienceToNextLevel,
            MaxHealth = stats.MaxHealth,
            CurrentHealth = stats.CurrentHealth,
            Attack = stats.Attack,
            Defense = stats.Defense,
        };

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(data));
    }

    public static PlayerStats Load()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            return null;
        }

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var data = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
        if (data == null)
        {
            return null;
        }

        return new PlayerStats
        {
            CharacterName = data.CharacterName,
            Level = data.Level,
            Experience = data.Experience,
            ExperienceToNextLevel = data.ExperienceToNextLevel,
            MaxHealth = data.MaxHealth,
            CurrentHealth = data.CurrentHealth,
            Attack = data.Attack,
            Defense = data.Defense,
        };
    }

    private class SaveData
    {
        public string CharacterName { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int ExperienceToNextLevel { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
    }
}
