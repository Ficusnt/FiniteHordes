using System.Collections.Generic;
using UnityEngine;

public class ConsoleCmd_FH : ConsoleCmdAbstract
{
    public override string getDescription()
    {
        return "Finite Hordes commands. Usage: fh <end|set|start|buff|debug>";
    }

    public override string getHelp()
    {
        return
            "fh end -> immediately ends the blood moon\n" +
            "fh set <number> -> sets remaining zombie counter\n" +
            "fh start -> forces a blood moon to start\n" +
            "fh buff -> applies the zombie counter buff\n" +
            "fh debug -> prints current state info";
    }

    public override string[] getCommands()
    {
        return new string[] { "fh" };
    }

    private static System.Collections.IEnumerator EndBloodMoonNextFrame()
    {
        yield return null;

        var bloodMoon = GameManager.Instance.World
            ?.GetAIDirector()
            ?.BloodMoonComponent;

        if (bloodMoon != null)
        {
            Debug.Log("[FiniteHordes] Ending blood moon via command.");
            bloodMoon.EndBloodMoon();
        }
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        if (_params == null || _params.Count == 0)
        {
            SdtdConsole.Instance.Output(GetHelp());
            return;
        }

        string sub = _params[0].ToLower();
        var world = GameManager.Instance.World;

        if (world == null)
        {
            SdtdConsole.Instance.Output("[FiniteHordes] World not found.");
            return;
        }

        var bloodMoon = world.GetAIDirector()?.BloodMoonComponent;

        switch (sub)
        {
            // END BLOOD MOON
            case "end":
            {
                if (bloodMoon == null)
                {
                    SdtdConsole.Instance.Output("[FiniteHordes] BloodMoonComponent not found.");
                    return;
                }

                BloodMoonState.forceEnd = true;
                BloodMoonState.cap = -1;

                GameManager.Instance.StartCoroutine(EndBloodMoonNextFrame());

                SdtdConsole.Instance.Output("[FiniteHordes] Blood moon forced to end.");
                break;
            }

            // SET COUNTER
            case "set":
            {
                if (_params.Count < 2 || !int.TryParse(_params[1], out int value))
                {
                    SdtdConsole.Instance.Output("Usage: fh set <number>");
                    return;
                }

                foreach (var player in world.Players.list)
                {
                    player.SetCVar("$BloodMoonZombies", value);
                }

                BloodMoonState.cap = value;

                SdtdConsole.Instance.Output($"[FiniteHordes] Remaining zombies set to {value}");
                break;
            }

            // START BLOOD MOON
            case "start":
            {
                if (bloodMoon == null)
                {
                    SdtdConsole.Instance.Output("[FiniteHordes] BloodMoonComponent not found.");
                    return;
                }

                BloodMoonState.forceEnd = false;

                bloodMoon.StartBloodMoon();

                SdtdConsole.Instance.Output("[FiniteHordes] Blood moon started.");
                break;
            }

            // DEBUG INFO
            case "debug":
            {
                float time = world.worldTime % 24000 / 1000f;

                float remaining = -1f;

                foreach (var player in world.Players.list)
                {
                    remaining = player.GetCVar("$BloodMoonZombies");
                    break;
                }

                SdtdConsole.Instance.Output(
                    "[FiniteHordes DEBUG]\n" +
                    $"Time: {time}\n" +
                    $"forceEnd: {BloodMoonState.forceEnd}\n" +
                    $"cap: {BloodMoonState.cap}\n" +
                    $"remaining: {remaining}"
                );
                break;
            }

            
            case "buff":
            {
                var players = BloodMoonState.GetRelevantPlayers(world);

                if (players.Count == 0)
                {
                    SdtdConsole.Instance.Output("[FiniteHordes] No players found.");
                    return;
                }

                foreach (var player in players)
                {
                    if (player.GetCVar("$BloodMoonZombies") <= 0f)
                        player.SetCVar("$BloodMoonZombies", 10f);

                    if (!player.Buffs.HasBuff("buffFiniteHordesCounter") && ModConfig.Instance.ShowZombieCount)
                        player.Buffs.AddBuff("buffFiniteHordesCounter");
                }

                SdtdConsole.Instance.Output("[FiniteHordes] Buff applied.");
                break;
            }

            default:
                SdtdConsole.Instance.Output("Unknown command. Use: fh <end|set|start|debug>");
                break;
        }
    }

}