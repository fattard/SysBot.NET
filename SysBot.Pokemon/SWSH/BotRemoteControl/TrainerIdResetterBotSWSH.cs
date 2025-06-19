using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

namespace SysBot.Pokemon;

public class TrainerIdResetterBotSWSH(PokeBotState Config) : PokeRoutineExecutor8SWSH(Config)
{
    Dictionary<string, uint> TrainerDataOffsetDic = new Dictionary<string, uint>()
    {
        { "1.0.0", 0x42935e48 },
        {   "1.1", 0x42935e48 },
        { "1.3.2", 0x45068F18 }
    };

    public override async Task MainLoop(CancellationToken token)
    {
        string[] TIDpatterns =
        [
            "8xxxxx",
            "0xxxxx",
        ];

        var sav = new SAV8SWSH();
        var info = sav.MyStatus;

        bool foundPattern = false;

        try
        {
            // Check if botbase is on the correct version or later.
            await VerifyBotbaseVersion(token).ConfigureAwait(false);

            // Switch Logo and game load screen
            await Task.Delay(12_000, token).ConfigureAwait(false);

            while (!token.IsCancellationRequested && !foundPattern)
            {
                // Check title so we can warn if mode is incorrect.
                string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
                if (title is not (PokeDataOffsetsSWSH.SwordID or PokeDataOffsetsSWSH.ShieldID))
                    throw new Exception($"{title} is not a valid SWSH title. Is your mode correct?");

                // Verify the game version.
                var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
                if (!TrainerDataOffsetDic.ContainsKey(game_version))
                    throw new Exception($"Game version {game_version} is not supported.");

                bool savePrepared = false;
                uint tid = 0;

                while (!savePrepared)
                {
                    if (token.IsCancellationRequested)
                        break;

                    await Task.Delay(1_000, token).ConfigureAwait(false);

                    var read = await Connection.ReadBytesAsync(TrainerDataOffsetDic[game_version], PokeDataOffsetsSWSH.TrainerDataLength, token).ConfigureAwait(false);
                    read.CopyTo(info.Data);

                    if (sav.DisplayTID != 0 && !string.IsNullOrEmpty(sav.OT))
                    {
                        savePrepared = true;
                        tid = sav.DisplayTID;

                        await Task.Delay(5_000, token).ConfigureAwait(false);
                    }

                    Log($"Waiting for user to choose character traits and name");
                }

                if (token.IsCancellationRequested)
                    break;

                uint last_tid = tid;

                Log($"Automating intro button presses...");

                while (last_tid == tid)
                {
                    if (token.IsCancellationRequested)
                        break;

                    await Click(B, 0_500, token).ConfigureAwait(false);

                    var read = await Connection.ReadBytesAsync(TrainerDataOffsetDic[game_version], PokeDataOffsetsSWSH.TrainerDataLength, token).ConfigureAwait(false);
                    read.CopyTo(info.Data);

                    last_tid = tid;
                    tid = sav.DisplayTID;
                }

                foreach (var t in TIDpatterns)
                {
                    if (MatchesPattern(sav.DisplayTID, t.ToLower()))
                    {
                        foundPattern = true;
                        break;
                    }
                }

                if (!foundPattern)
                {
                    await CloseGame(token);
                    await StartGame(token);
                }
            }

            Log($"TrainerID: {sav.DisplayTID:D6}");
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"Ending {nameof(TrainerIdResetterBotSWSH)} loop.");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task HardStop()
    {
        await SetStick(SwitchStick.LEFT, 0, 0, 0_500, CancellationToken.None).ConfigureAwait(false); // reset
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task CloseGame(CancellationToken token)
    {
        // Close out of the game
        await Click(B, 0_500, token).ConfigureAwait(false);
        await Click(HOME, 2_000, token).ConfigureAwait(false);
        await Click(X, 1_000, token).ConfigureAwait(false);
        await Click(A, 5_000, token).ConfigureAwait(false);
        Log("Closed out of the game!");
    }

    public async Task StartGame(CancellationToken token)
    {
        // Open game.
        await Click(A, 1_000, token).ConfigureAwait(false);

        await Click(A, 1_000, token).ConfigureAwait(false);
        // If they have DLC on the system and can't use it, requires pressing UP + A to start the game.
        // Should be harmless otherwise since they'll be in loading screen.
        await Click(DUP, 0_600, token).ConfigureAwait(false);
        await Click(A, 0_600, token).ConfigureAwait(false);

        Log("Restarting the game!");

        // Switch Logo and game load screen
        await Task.Delay(12_000, token).ConfigureAwait(false);
    }

    bool MatchesPattern(uint tid, string pattern)
    {
        Log($"Checking {tid:D6} against pattern {pattern}");

        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 'x' && uint.Parse($"{pattern[i]}") != GetDigit(tid, i))
            {
                return false;
            }
        }

        return true;
    }

    static uint GetDigit(uint val, int c)
    {
        for (int i = 5; i > c; i--)
            val /= 10;
        return val % 10;
    }
}


