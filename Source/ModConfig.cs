using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class ModConfig
{
    // Hard cap on total zombies per blood moon regardless of game stage.
    // Default 1000 matches original mod's cap at GS 250.
    public int MaxHordeNightZombies { get; set; } = 1000;

    // Replaces the hardcoded *4 multiplier from the original mod.
    // Total zombies = partyGS * StartingWeight, capped at MaxHordeNightZombies.
    // Default 4.0 matches original behaviour (GS 100 = 400 zombies).
    public float StartingWeight { get; set; } = 4.0f;

    // Multiplier applied to game stage scaling inside the spawner.
    // 1.0 = vanilla scaling.
    public float GameStageZombieMultiplier { get; set; } = 1.0f;

    // Whether to show the zombie counter buff on the HUD.
    public bool ShowZombieCount { get; set; } = true;

    private static ModConfig _instance;
    public static ModConfig Instance => _instance ?? (_instance = new ModConfig());

    public static void LoadConfig(string modDir)
    {
        string path = Path.Combine(modDir, "Config", "settings.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            _instance = JsonConvert.DeserializeObject<ModConfig>(json);
            Debug.Log(string.Format("[FiniteHordes] Config loaded: MaxZombies={0}, StartingWeight={1}, GSMultiplier={2}, ShowCount={3}",
                _instance.MaxHordeNightZombies,
                _instance.StartingWeight,
                _instance.GameStageZombieMultiplier,
                _instance.ShowZombieCount));
        }
        else
        {
            _instance = new ModConfig();
            string json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
            File.WriteAllText(path, json);
            Debug.LogWarning("[FiniteHordes] Config not found, created defaults at: " + path);
        }
    }
}