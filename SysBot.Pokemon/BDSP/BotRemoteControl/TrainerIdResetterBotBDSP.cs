using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

namespace SysBot.Pokemon;

public class TrainerIdResetterBotBDSP(PokeBotState Config) : PokeRoutineExecutor8BS(Config)
{
    Dictionary<string, IReadOnlyList<long>> MyStatusTIDPointerDic_BD = new Dictionary<string, IReadOnlyList<long>>
    {
        { "1.1.1", [0x4C1DCF8, 0xB8, 0x10, 0xE8] },
        { "1.1.2", [0x4E34DD0, 0xB8, 0x10, 0xE8] },
        { "1.1.3", [0x4E59E60, 0xB8, 0x10, 0xE8] },
        { "1.2.0", [0x4E36C58, 0xB8, 0x10, 0xE8] },
        { "1.3.0", [0x4C64DC0, 0xB8, 0x10, 0xE8] },
    };

    Dictionary<string, IReadOnlyList<long>> MyStatusTIDPointerDic_SP = new Dictionary<string, IReadOnlyList<long>>
    {
        { "1.1.1", [0x4E34DD0, 0xB8, 0x10, 0xE8] },
        { "1.1.2", [0x4E34DD0, 0xB8, 0x10, 0xE8] },
        { "1.1.3", [0x4E59E60, 0xB8, 0x10, 0xE8] },
        { "1.2.0", [0x4E36C58, 0xB8, 0x10, 0xE8] },
        { "1.3.0", [0x4E7BE98, 0xB8, 0x10, 0xE8] },
    };

    Dictionary<string, IReadOnlyList<long>> OffsetsDic;

    public override async Task MainLoop(CancellationToken token)
    {
        string[] TIDpatterns =
        [
            "8xxxxx",
            "0xxxxx",
        ];

        var sav = new SAV8BS();
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
                // Pull title so we know which set of offsets to use.
                string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
                OffsetsDic = title switch
                {
                    BasePokeDataOffsetsBS.BrilliantDiamondID => MyStatusTIDPointerDic_BD,
                    BasePokeDataOffsetsBS.ShiningPearlID => MyStatusTIDPointerDic_SP,
                    _ => throw new Exception($"{title} is not a valid Pokémon BDSP title. Is your mode correct?"),
                };

                // Verify the game version.
                var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
                if (!OffsetsDic.ContainsKey(game_version))
                    throw new Exception($"Game version {game_version} is not supported.");

                bool savePrepared = false;
                uint tidVal = 0;

                while (!savePrepared)
                {
                    if (token.IsCancellationRequested)
                        break;

                    await Task.Delay(1_000, token).ConfigureAwait(false);

                    var offset = await SwitchConnection.PointerAll(OffsetsDic[game_version], token).ConfigureAwait(false);
                    var tid = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 2, token).ConfigureAwait(false);
                    var sid = await SwitchConnection.ReadBytesAbsoluteAsync(offset + 2, 2, token).ConfigureAwait(false);

                    info.TID16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(tid);
                    info.SID16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sid);

                    if (sav.DisplayTID != 0)
                    {
                        savePrepared = true;
                        tidVal = sav.DisplayTID;
                    }
                }

                if (token.IsCancellationRequested)
                    break;

                // Ensure it is stable
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                    var offset = await SwitchConnection.PointerAll(OffsetsDic[game_version], token).ConfigureAwait(false);
                    var tid = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 2, token).ConfigureAwait(false);
                    var sid = await SwitchConnection.ReadBytesAbsoluteAsync(offset + 2, 2, token).ConfigureAwait(false);

                    info.TID16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(tid);
                    info.SID16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sid);

                    if (tidVal != sav.DisplayTID)
                    {
                        Log($"TrainerID unstable");
                    }
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

        Log($"Ending {nameof(TrainerIdResetterBotBDSP)} loop.");
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

