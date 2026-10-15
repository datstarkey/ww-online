using Serilog;
using WWOnline.Native;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// <see cref="IPlayInventory"/> on the native game: tww.h's shared-inventory calls (<see cref="ITwwInvApi"/>), each
/// read or check-and-write in one call on the game thread, so the game can't apply a change between them.
/// </summary>
public sealed class NativePlayInventory(INativeGame game) : IPlayInventory
{
    private static readonly ILogger Logger = Log.ForContext<NativePlayInventory>();

    public int[]? ReadPending() => Call(api =>
    {
        var play = api.InvGet();
        return ((ReadOnlySpan<int>)play.Pending).ToArray();
    });

    public bool Add(TwwInvCounter counter, int delta) => Call(api =>
    {
        api.InvAdd(counter, delta);
        return true;
    });

    public byte[]? ReadSelectItems() => Call(api =>
    {
        var play = api.InvGet();
        return ((ReadOnlySpan<byte>)play.SelectItem)[..BagMemory.ButtonCount].ToArray();
    });

    public bool WriteSelectItems(byte[] items) => Call(api =>
    {
        for (int b = 0; b < Math.Min(items.Length, BagMemory.ButtonCount); b++) api.InvSetSelectItem(b, items[b]);
        return true;
    });

    public (int? Slot, bool KeyHud) ReadStage() => Call(api =>
    {
        var play = api.InvGet();
        return play.SaveTbl >= 0 && play.SaveTbl < StageFlags.SlotCount ? ((int?)play.SaveTbl, play.KeyDisp != 0) : (null, false);
    });

    /// <summary>Always 0: the native puppet never swaps a peer's equipment into the local save.</summary>
    public uint? ReadEquipSwapSeq() => 0;

    private T? Call<T>(Func<ITwwInvApi, T> func)
    {
        try
        {
            return game.Invoke(api => func((ITwwInvApi)api));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            Logger.Debug(ex, "[native] inventory call failed");
            return default;
        }
    }
}
