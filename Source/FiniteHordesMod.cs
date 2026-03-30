using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class FiniteHordesMod : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        ModConfig.LoadConfig(_modInstance.Path);

        Harmony harmony = new Harmony("com.finitehordes.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        ModEvents.EntityKilled.RegisterHandler(OnEntityKilled);
        Debug.Log("[FiniteHordes] Initialized. Harmony patches applied.");
    }

    private static System.Collections.IEnumerator EndBloodMoonNextFrame()
    {
        yield return null; // wait 1 frame

        try
        {
            var bloodMoon = GameManager.Instance.World
                ?.GetAIDirector()
                ?.BloodMoonComponent;

            if (bloodMoon != null)
            {
                Debug.Log("[FiniteHordes] Ending blood moon (delayed).");
                bloodMoon.EndBloodMoon();
            }
            else
            {
                Debug.LogWarning("[FiniteHordes] Delayed End: BloodMoonComponent not found.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FiniteHordes] Delayed End failed: " + e.Message);
        }
    }

    private static void OnEntityKilled(ref ModEvents.SEntityKilledData data)
    {
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

        World world = GameManager.Instance.World;
        if (world == null) return;

        // Only count blood moon enemy kills
        EntityEnemy killedEnemy = data.KilledEntitiy as EntityEnemy;
        if (killedEnemy == null || !killedEnemy.IsBloodMoon) return;

        float current = -1f;

        var players = BloodMoonState.GetRelevantPlayers(world);
        if (players.Count == 0) return;

        current = Mathf.Max(players[0].GetCVar("$BloodMoonZombies"), 0f);

        if (current <= 0f) return;

        float remaining = Mathf.Max(current - 1f, 0);

        // Update player counter
        foreach (var player in players)
        {
            player.SetCVar("$BloodMoonZombies", remaining);
            BloodMoonState.ManageBuff(player,"add");
        }

        Debug.Log(string.Format("[FiniteHordes] Zombie killed. Remaining: {0}", remaining));

        // All zombies killed — force end the blood moon
        if (remaining <= 0f && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            Debug.Log("[FiniteHordes] All zombies killed. Ending blood moon.");
            try
            {
                AIDirectorBloodMoonComponent bloodMoon = GameManager.Instance.World?.GetAIDirector()?.BloodMoonComponent;
                if (bloodMoon != null)
                {
                    BloodMoonState.forceEnd = true;
                    BloodMoonState.cap = -1;
                    GameManager.Instance.StartCoroutine(EndBloodMoonNextFrame());
                }
                else
                    Debug.LogWarning("[FiniteHordes] BloodMoonComponent not found, cannot end blood moon.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FiniteHordes] Failed to end blood moon: " + e.Message);
            }
        }
    }
}