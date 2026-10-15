namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwWorldApi
    {
        public int WorldSaveSlot() => TwwNative.tww_world_save_slot();

        public int WorldRoomZone(int room) => TwwNative.tww_world_room_zone(room);

        public int WorldSalvageRead(Span<TwwWorldSalvagePoint> points)
        {
            fixed (TwwWorldSalvagePoint* p = points) return (int)TwwNative.tww_world_salvage_read(p, (uint)points.Length);
        }

        public bool WorldSalvageSetFlag(int index, byte bits) => TwwNative.tww_world_salvage_set_flag((uint)index, bits) == 1;

        public void WorldSetRemoteItems(int slot, uint mask) => TwwNative.tww_world_set_remote_items(slot, mask);

        public TwwWorldStats WorldStats()
        {
            TwwWorldStats stats;
            TwwNative.tww_world_get_stats(&stats);
            return stats;
        }

        public void WorldLivePost(in TwwWorldLiveBatch batch)
        {
            fixed (TwwWorldLiveBatch* p = &batch) TwwNative.tww_world_live_post(p);
        }

        public TwwWorldLiveStatus WorldLiveStatus()
        {
            TwwWorldLiveStatus status;
            TwwNative.tww_world_live_status(&status);
            return status;
        }
    }
}
