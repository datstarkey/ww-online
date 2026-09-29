using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// Game-state changes done the way the game itself does them, so the HUD and game logic see
/// them — prefer these over writing the raw save value.
/// </summary>
public static class GameActions
{
    /// <summary>
    /// Mirror of dComIfGp_setItemRupeeCount(delta): queue a rupee change that d_meter applies on
    /// the next frame (clamped to the wallet size) with the counter animation + sound.
    /// </summary>
    public static bool AddRupees(IDolphinService dolphin, int delta)
    {
        if (!dolphin.IsConnected || delta == 0) return false;
        int pending = dolphin.Read(GameMemoryAddresses.Player.PendingRupeeDelta) ?? 0;
        return dolphin.Write(GameMemoryAddresses.Player.PendingRupeeDelta, pending + delta);
    }

    /// <summary>Set rupees to <paramref name="target"/> via the HUD path (see <see cref="AddRupees"/>).</summary>
    public static bool SetRupees(IDolphinService dolphin, int target)
    {
        if (!dolphin.IsConnected) return false;
        int current = dolphin.Read(GameMemoryAddresses.Player.RupeeCount) ?? 0;
        int pending = dolphin.Read(GameMemoryAddresses.Player.PendingRupeeDelta) ?? 0;
        return AddRupees(dolphin, target - (current + pending));
    }

    /// <summary>
    /// Set arrows to <paramref name="target"/> via dComIfGp_setItemArrowNumCount's pending count,
    /// so d_meter updates the HUD counter (and clamps to the quiver's max) like a pickup does.
    /// </summary>
    public static bool SetArrows(IDolphinService dolphin, int target) =>
        SetAmmo(dolphin, GameMemoryAddresses.Player.CurrentArrowCount, GameMemoryAddresses.Player.PendingArrowDelta, target);

    /// <summary>Set bombs to <paramref name="target"/> via the HUD path (see <see cref="SetArrows"/>).</summary>
    public static bool SetBombs(IDolphinService dolphin, int target) =>
        SetAmmo(dolphin, GameMemoryAddresses.Player.CurrentBombCount, GameMemoryAddresses.Player.PendingBombDelta, target);

    private static bool SetAmmo(IDolphinService dolphin, MemoryAddress<byte> count, MemoryAddress<short> pendingDelta, int target)
    {
        if (!dolphin.IsConnected) return false;
        int current = dolphin.Read(count) ?? 0;
        int pending = dolphin.Read(pendingDelta) ?? 0;
        int delta = Math.Clamp(target, 0, 99) - (current + pending);
        return delta == 0 || dolphin.Write(pendingDelta, (short)(pending + delta));
    }
}
