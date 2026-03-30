using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

// --------------------------------------------------------
//  Runtime state
// --------------------------------------------------------
public static class BloodMoonState
{
    public static bool forceEnd   = false;    // set by kill handler before calling EndBloodMoon
    public static int  cap        = -1;

    // Start window padding (minutes)
    public static float startWindowPaddingMinutes = 5f;

    // Helper: determines if we are inside the allowed bloodmoon start window
    public static bool IsWithinBloodMoonStartWindow(World world)
    {
        if (world == null) return false;

        float hour = world.worldTime % 24000 / 1000f;
        float dusk = world.DuskHour;

        float padding = startWindowPaddingMinutes / 60f;

        float windowStart = dusk - padding;
        float windowEnd   = dusk + padding;

        return (hour >= windowStart && hour <= windowEnd);
    }

    // Helper: determines if we are inside the allowed bloodmoon end window
    public static bool IsWithinBloodMoonEndWindow(World world)
    {
        if (world == null) return false;

        float hour = world.worldTime % 24000 / 1000f;
        float dawn = world.DawnHour;
        float dusk = world.DuskHour;

        return hour >= dawn && hour <= dusk;
    }

    public static List<EntityPlayer> GetRelevantPlayers(World world)
    {
        var players = new List<EntityPlayer>();

        if (world == null) return players;

        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            foreach (var p in world.Players.list)
            {
                if (p != null)
                    players.Add(p);
            }
        }
        else
        {
            var p = world.GetPrimaryPlayer();
            if (p != null)
                players.Add(p);
        }

        return players;
    }

    public static void ManageBuff(EntityPlayer player, string opt)
    {
        if (player == null) return;

        switch(opt)
        {
            case "add":
                if (ModConfig.Instance.ShowZombieCount)
                {
                    player.Buffs.AddBuff("buffFiniteHordesCounter");
                }
                return;

            case "rem":
                if(player.Buffs.HasBuff("buffFiniteHordesCounter"))
                {
                    player.Buffs.RemoveBuff("buffFiniteHordesCounter");
                }
                return;
        }
    }
}


// -----------------------------------------------------------------------
// Patch 1: GameManager.StartGame
// Resets $BloodMoonZombies CVar to -1 on game start as a clean slate.
// -----------------------------------------------------------------------
[HarmonyPatch(typeof(GameManager), "StartGame")]
public static class Patch_GameManager_StartGame
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {   
            World world = GameManager.Instance.World;
            if (world == null) return;

           var players = BloodMoonState.GetRelevantPlayers(world);
            foreach (var player in players)
            {
                if (player)
                {
                    player.SetCVar("$BloodMoonZombies", -1f);
                    BloodMoonState.ManageBuff(player, "rem");
                }
            }

            BloodMoonState.cap = -1;
            BloodMoonState.forceEnd = true;
            Debug.Log("[FiniteHordes] $BloodMoonZombies CVar and counters reset on StartGame.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FiniteHordes] StartGame_Postfix failed: " + e.Message);
        }
    }
}

// -----------------------------------------------------------------------
// Patch 2: AIDirectorBloodMoonComponent.IsBloodMoonTime
// Forces blood moon to stay active while zombies remain.
// -----------------------------------------------------------------------
[HarmonyPatch(typeof(AIDirectorBloodMoonComponent), "IsBloodMoonTime")]
public static class Patch_IsBloodMoonTime
{
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result)
    {
        try
        {
            if (BloodMoonState.forceEnd)
            {
                __result = false;
                return false;
            }

            World world = GameManager.Instance.World;
            if (world == null) return true;

            // Check your actual remaining counter
            float remaining = -1;
            var players = BloodMoonState.GetRelevantPlayers(world);
            if (players.Count == 0) return true;

            remaining = players[0].GetCVar("$BloodMoonZombies");

            if (remaining > 0f)
            {
                __result = true; //it's still bloodmoon
                return false; // force blood moon ON
            }

            return true; // let vanilla end it
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FiniteHordes] IsBloodMoonTime failed: " + e.Message);
            return true;
        }
    }
}


// -----------------------------------------------------------------------
// Patch 3: AIDirectorBloodMoonParty.InitParty
// - Sets $BloodMoonZombies CVar and applies the counter buff
// - Sets the cap and the gamestage.
// -----------------------------------------------------------------------
[HarmonyPatch(typeof(AIDirectorBloodMoonParty), "InitParty")]
public static class Patch_AIDirectorBloodMoonParty_InitParty
{
    [HarmonyPostfix]
    public static void Postfix(AIDirectorBloodMoonParty __instance)
    {
        try
        {
            int partyGS = __instance.partySpawner.CalcPartyLevel();

            int cap = Mathf.Min(
                Mathf.RoundToInt(partyGS * ModConfig.Instance.StartingWeight),
                ModConfig.Instance.MaxHordeNightZombies
            );

            cap = Mathf.Max(cap, 1);

            BloodMoonState.cap = cap;

            __instance.partySpawner.gsScaling =
                ModConfig.Instance.GameStageZombieMultiplier;

            Debug.Log(string.Format(
                "[FiniteHordes] InitParty patched: partyGS={0}, spawnCap={1}, gsScaling={2}",
                partyGS,
                cap,
                __instance.partySpawner.gsScaling));

           
                World world = GameManager.Instance.World;
                if (world == null) return;

                var players = BloodMoonState.GetRelevantPlayers(world);
                foreach (var player in players)
                {
                    player.SetCVar("$BloodMoonZombies", cap);
                    BloodMoonState.ManageBuff(player,"add");
                   
                }
            
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FiniteHordes] InitParty_Postfix failed: " + e.Message);
        }
    }
}


// -----------------------------------------------------------------------
// Patch 4: AIDirectorBloodMoonComponent.StartBloodMoon
// Allows start only near dusk window and resets UI counter.
// -----------------------------------------------------------------------
[HarmonyPatch(typeof(AIDirectorBloodMoonComponent), "StartBloodMoon")]
public static class Patch_StartBloodMoonWindow
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        World world = GameManager.Instance.World;
        if (world == null) return true;

        if (BloodMoonState.forceEnd)
        {
            if (!BloodMoonState.IsWithinBloodMoonStartWindow(world))
            {
                Debug.Log("[FiniteHordes] StartBloodMoon blocked.");
                return false;
            }
            else
            {
                Debug.Log("[FiniteHordes] StartBM at BM time Resetting forceEnd and letting through.");
                BloodMoonState.forceEnd = false;
            }
        }

        var players = BloodMoonState.GetRelevantPlayers(world);
        foreach (var player in players)
        {
            player.SetCVar("$BloodMoonZombies", -1f);
            BloodMoonState.ManageBuff(player,"add");
        }

        return true;
    }
}


// -----------------------------------------------------------------------
// Patch 5: AIDirectorBloodMoonComponent.EndBloodMoon
// Only allow ending when we explicitly decide (counter reached 0)
// -----------------------------------------------------------------------
[HarmonyPatch(typeof(AIDirectorBloodMoonComponent), "EndBloodMoon")]
public static class Patch_AIDirectorBloodMoonComponent_EndBloodMoon
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        try
        {
            World world = GameManager.Instance.World;
            if (world == null) return true;

            // If we still have zombies to kill → block ending
            if (!BloodMoonState.forceEnd)
            {
                return false;
            }

            // We decided to end → allow vanilla
            Debug.Log("[FiniteHordes] Blood moon ending (quota reached).");

            BloodMoonState.forceEnd = true;
            BloodMoonState.cap = -1;

            var players = BloodMoonState.GetRelevantPlayers(world);
            foreach (var player in players)
            {
                player.SetCVar("$BloodMoonZombies", 0f);
                BloodMoonState.ManageBuff(player,"rem");
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FiniteHordes] EndBloodMoon failed: " + e.Message);
            return true;
        }
    }
}

// -----------------------------------------------------------------------
// Patch 6: AIDirectorBloodMoonParty.SpawnZombie
// Prevents new spawns after we end the bloodmoon or
// keeps them on until counter is zero.
// -----------------------------------------------------------------------
[HarmonyPatch(typeof(AIDirectorBloodMoonParty), "SpawnZombie")]
public class Patch_AIDirectorBloodMoonParty_SpawnZombie
{
    [HarmonyPrefix]
    static bool Prefix()
    {
        if (BloodMoonState.forceEnd)
        {
            return false; // stop spawning
        }

        return true;
    }
}

