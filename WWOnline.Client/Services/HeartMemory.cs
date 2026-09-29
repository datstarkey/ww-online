using WWOnline.Data;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>What the game looks like this tick, for <see cref="HeartReconciler"/>.</summary>
/// <param name="Flags">Every heart flag: the current stage's memBits from the live copy, the rest from the save.</param>
/// <param name="MaxLife">dSv_player_status_a_c::mMaxLife (quarter hearts).</param>
/// <param name="Idle">Nothing is about to change the max on its own: no max-life change queued for the HUD, no event
/// (a get-item demo, a reward's talk), no pause menu, no minigame.</param>
/// <param name="Stage">Stage name and room, for the logs.</param>
public sealed record HeartObservation(HeartFlagState Flags, int MaxLife, int Life, bool Idle, string Stage = "", int Room = -1);

/// <summary>
/// Reads the heart flags and max health (<see cref="GameMemoryAddresses.Hearts"/>) and makes
/// <see cref="HeartReconciler"/>'s writes. The saved copy of the current stage's memBits is stale (putSave
/// overwrites it when the stage is left, d_stage.cpp:2255), so the live copy is read for that slot.
/// </summary>
public static class HeartMemory
{
    private const int SlotCount = HeartFlagState.SlotCount;

    /// <summary>Read the whole state, or null when the save can't be read.</summary>
    public static HeartObservation? Read(IDolphinService dolphin)
    {
        var save = dolphin.ReadMemory(GameMemoryAddresses.GameInfo, GameMemoryAddresses.Hearts.SaveBlockLength);
        if (save == null || save.Length < GameMemoryAddresses.Hearts.SaveBlockLength) return null;
        var (slot, _) = SmallKeyMemory.ReadStage(dolphin);
        var live = slot.HasValue
            ? dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.LiveMemory, GameMemoryAddresses.WorldFlags.MemorySize)
            : null;

        var flags = Parse(save, slot, live);
        int maxLife = U16(save, GameMemoryAddresses.Hearts.OffMaxLife);
        int life = U16(save, GameMemoryAddresses.Hearts.OffLife);

        var pending = dolphin.Read(GameMemoryAddresses.Hearts.PendingMaxLife);
        var evt = dolphin.ReadMemory(GameMemoryAddresses.Events.EventMode, 1);
        var menu = dolphin.ReadMemory(GameMemoryAddresses.Events.MenuPause, 1);
        var minigame = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.MiniGameType, 1);
        bool idle = pending == 0 && evt is [0] && menu is [0] && minigame is [0];

        var name = dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentStageName.Address, 8);
        string stage = name == null ? "" : System.Text.Encoding.ASCII.GetString(name, 0, Array.IndexOf(name, (byte)0) is int n and >= 0 ? n : name.Length);
        int room = dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, 1) is [var r] ? (sbyte)r : -1;

        return new HeartObservation(flags, maxLife, life, idle, stage, room);
    }

    /// <summary>The heart flags in a gameInfo save block (<see cref="GameMemoryAddresses.Hearts.SaveBlockLength"/> bytes),
    /// with <paramref name="live"/> (the live dSv_memory_c) standing in for <paramref name="currentSlot"/>.</summary>
    public static HeartFlagState Parse(byte[] save, int? currentSlot, byte[]? live)
    {
        var f = new HeartFlagState();
        for (int i = 0; i < SlotCount; i++)
        {
            bool isLive = i == currentSlot && live is { Length: >= GameMemoryAddresses.WorldFlags.MemorySize };
            var src = isLive ? live! : save;
            int off = isLive ? 0 : GameMemoryAddresses.Hearts.OffSavedMemory + i * GameMemoryAddresses.WorldFlags.MemorySize;
            f.Tbox[i] = U32(src, off + GameMemoryAddresses.WorldFlags.OffTbox);
            f.Item[i] = U32(src, off + GameMemoryAddresses.WorldFlags.OffItem);
            f.DungeonItem[i] = src[off + GameMemoryAddresses.WorldFlags.OffDungeonItem];
        }
        Array.Copy(save, GameMemoryAddresses.Hearts.OffEvents, f.Events, 0, HeartFlagState.EventBytes);
        for (int w = 0; w < HeartFlagState.MapWords; w++)
            f.CompleteMaps[w] = U32(save, GameMemoryAddresses.Hearts.OffCompleteMaps + w * 4);
        for (int g = 0; g < HeartFlagState.OceanGrids; g++)
            f.Ocean[g] = (ushort)U16(save, GameMemoryAddresses.Hearts.OffOcean + g * 2);
        f.GetBagReserve = U32(save, GameMemoryAddresses.Hearts.OffGetBagReserve);
        Array.Copy(save, GameMemoryAddresses.Hearts.OffDeliveryBag, f.DeliveryBag, 0, HeartFlagState.BagSlots);
        return f;
    }

    /// <summary>
    /// Queue a max-life change for the HUD the way a Piece of Heart does (play.mItemMaxLifeCount): d_meter applies it
    /// next frame, clamped to 0..80, refilling life on a gain and clamping it on a loss, and animates the heart row.
    /// Only while nothing is queued (the game's own += would race it); re-checked just before the write.
    /// </summary>
    public static bool ApplyDelta(IDolphinService dolphin, int delta)
    {
        if (delta == 0) return true;
        if (dolphin.Read(GameMemoryAddresses.Hearts.PendingMaxLife) != 0) return false;
        return dolphin.Write(GameMemoryAddresses.Hearts.PendingMaxLife, (short)delta);
    }

    private static int U16(byte[] b, int o) => b[o] << 8 | b[o + 1];
    private static uint U32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
}
