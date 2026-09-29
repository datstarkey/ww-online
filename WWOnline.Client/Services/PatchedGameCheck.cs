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
/// runs without the puppet REL) writes a FourCC to PuppetLayout.STATUS_ADDR every time it draws Link:
/// "HOOK", "TITL" (title screen), "NAME" (file select) or "DONE". An unpatched game never writes there.
/// Attach only succeeds once a save's data is in memory, so Link is drawn within a few frames.</item>
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
        /// <summary>No draw-hook status word: not our patched game (or Link not drawn yet).</summary>
        NotPatched,
    }

    /// <summary>MEM1 the patched game needs: bi2.bin is patched to 48 MB (GamePatcherService.PatchBi2).</summary>
    public const long RequiredMem1Size = 0x3000000;

    /// <summary>link_draw_hook.c STATUS_HOOK / STATUS_TITLE / STATUS_NAME / STATUS_DONE, as stored (big-endian ASCII).</summary>
    public static readonly string[] HookStatuses = ["HOOK", "TITL", "NAME", "DONE"];

    public static Result Check(IDolphinService dolphin)
    {
        if (dolphin.EmulatedMemorySize is long size && size < RequiredMem1Size) return Result.SmallMemory;
        return IsHookStatus(dolphin.ReadMemory(PuppetLayout.STATUS_ADDR, 4)) ? Result.Ok : Result.NotPatched;
    }

    public static bool IsHookStatus(byte[]? word) =>
        word is { Length: 4 } && HookStatuses.Contains(System.Text.Encoding.ASCII.GetString(word), StringComparer.Ordinal);
}
