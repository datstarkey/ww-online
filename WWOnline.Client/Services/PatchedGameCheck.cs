using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// Is the game running in the Dolphin we just attached to our patched build, set up the way it needs?
/// Checked before the sync starts writing to the scratch area (PuppetLayout.SCRATCH_REGION_START..END),
/// which in any other game (a PAL or Japanese disc, the unpatched US game) is memory the game owns.
/// <list type="bullet">
/// <item><b>48 MB MEM1</b>: the patched game needs Dolphin's memory override (it hangs on a black screen
/// without it). The region size comes from the attach's memory scan; unknown sizes aren't held against it.</item>
/// <item><b>Our draw hook</b>: the patched main.dol's Link draw hook (link_draw_hook.c, in the DOL, so it
/// runs without the puppet REL) writes one of PuppetLayout.STATUS_HOOK / STATUS_TITLE / STATUS_NAME /
/// STATUS_DONE (puppet_shared.h) to PuppetLayout.STATUS_ADDR every time it draws Link. An unpatched game
/// never writes there. Link isn't drawn on the title screen or file select, so with no save loaded yet
/// (dSv_player_status_a_c::mMaxLife still 0) a missing status is <see cref="Result.NotInGame"/>: wait,
/// don't call the game unpatched.</item>
/// </list>
/// </summary>
public static class PatchedGameCheck
{
    public enum Result
    {
        /// <summary>48 MB (or unknown) and the draw hook has run: the patched game.</summary>
        Ok,
        /// <summary>No game to attach to yet.</summary>
        NotBooted,
        /// <summary>MEM1 is smaller than 48 MB: Dolphin started without the override.</summary>
        SmallMemory,
        /// <summary>No draw-hook status word with a save loaded: not our patched game.</summary>
        NotPatched,
        /// <summary>No draw-hook status word yet, but no save is loaded either (title screen, file select):
        /// Link hasn't been drawn, so it can't tell yet.</summary>
        NotInGame,
    }

    /// <summary>MEM1 the patched game needs: bi2.bin is patched to 48 MB (GamePatcherService.PatchBi2).</summary>
    public const long RequiredMem1Size = 0x3000000;

    /// <summary>The draw hook's STATUS_ADDR values (puppet_shared.h, big-endian u32 FourCCs).</summary>
    public static readonly uint[] HookStatuses =
    [
        (uint)PuppetLayout.STATUS_HOOK, (uint)PuppetLayout.STATUS_TITLE, (uint)PuppetLayout.STATUS_NAME, (uint)PuppetLayout.STATUS_DONE,
    ];

    public static Result Check(IDolphinService dolphin)
    {
        if (dolphin.EmulatedMemorySize is long size && size < RequiredMem1Size) return Result.SmallMemory;
        if (IsHookStatus(dolphin.ReadMemory(PuppetLayout.STATUS_ADDR, 4))) return Result.Ok;
        // mMaxLife is 0 until a save is loaded (a real save always has at least 3 hearts); an unreadable
        // value counts as loaded, so an unpatched game is still refused.
        return dolphin.ReadMemory(GameMemoryAddresses.Player.MaxHealth.Address, 2) is [0, 0] ? Result.NotInGame : Result.NotPatched;
    }

    /// <summary>Is this 4-byte read of STATUS_ADDR (as stored in the game: big-endian) one of <see cref="HookStatuses"/>?</summary>
    public static bool IsHookStatus(byte[]? word) =>
        word is { Length: 4 } && HookStatuses.Contains(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(word));
}
