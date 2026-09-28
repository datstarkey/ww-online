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
}
