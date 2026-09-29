using WWOnline.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public class DungeonsCardViewModelTests
{
    private sealed class FakeSource : IDungeonKeysSource
    {
        public DungeonKeysSnapshot Snapshot { get; set; } = DungeonKeysSnapshot.None;
        public event Action? SnapshotChanged;
        public int Subscribers => SnapshotChanged?.GetInvocationList().Length ?? 0;
        public void Raise() => SnapshotChanged?.Invoke();
    }

    private static DungeonKeysSnapshot Snapshot(bool shared, params DungeonProgress[] dungeons) =>
        new(true, shared, dungeons.ToDictionary(d => d.Slot));

    private static DungeonsCardViewModel Vm(FakeSource source) => new(source, a => a());

    private static DungeonRowViewModel Row(DungeonsCardViewModel vm, int slot) => vm.Rows.Single(r => r.Slot == slot);

    [Fact]
    public void ListsTheFiveKeyDungeons()
    {
        using var vm = Vm(new FakeSource());
        Assert.Equal([3, 4, 5, 6, 7], vm.Rows.Select(r => r.Slot));
        Assert.Equal("Dragon Roost Cavern", Row(vm, 3).Name);
        Assert.Equal("Wind Temple", Row(vm, 7).Name);
    }

    [Fact]
    public void WithoutAGame_ShowsDashes()
    {
        using var vm = Vm(new FakeSource());
        Assert.False(vm.HasGame);
        Assert.False(vm.IsShared);
        Assert.Equal("No game", vm.StateText);
        Assert.All(vm.Rows, r => Assert.Equal("—", r.KeysText));
        Assert.All(vm.Rows, r => Assert.False(r.HasMap || r.HasCompass || r.HasBossKey || r.BossBeaten || r.HasKeys));
    }

    [Fact]
    public void Shared_ShowsTheDerivedKeys_TheirBreakdown_AndTheDungeonItems()
    {
        var source = new FakeSource
        {
            Snapshot = Snapshot(shared: true,
                new DungeonProgress(3, Keys: 1, KeysTaken: 3, KeysTotal: 4, DoorsOpened: 2, DoorsTotal: 4, DungeonItem: 0b0011),
                new DungeonProgress(4, Keys: 0, KeysTaken: 1, KeysTotal: 1, DoorsOpened: 1, DoorsTotal: 1, DungeonItem: 0b1111)),
        };
        using var vm = Vm(source);
        Assert.True(vm.IsShared);
        Assert.Equal("Shared", vm.StateText);

        var drc = Row(vm, 3);
        Assert.Equal("1", drc.KeysText);
        Assert.True(drc.HasKeys);
        Assert.Equal("3 of 4 keys found · 2 of 4 doors unlocked", drc.DetailText);
        Assert.True(drc.HasMap);
        Assert.True(drc.HasCompass);
        Assert.False(drc.HasBossKey);
        Assert.False(drc.BossBeaten);

        var fw = Row(vm, 4);
        Assert.Equal("0", fw.KeysText);
        Assert.False(fw.HasKeys);
        Assert.Equal("1 of 1 key found · 1 of 1 door unlocked", fw.DetailText);
        Assert.True(fw.HasBossKey);
        Assert.True(fw.BossBeaten);

        Assert.Equal("—", Row(vm, 5).KeysText); // no data for it
    }

    [Fact]
    public void RuleOff_ShowsThisGamesOwnKeys()
    {
        var source = new FakeSource { Snapshot = Snapshot(shared: false, new DungeonProgress(6, 2, null, null, null, null, 0)) };
        using var vm = Vm(source);
        Assert.False(vm.IsShared);
        Assert.Equal("Off: per player", vm.StateText);
        Assert.Equal("2", Row(vm, 6).KeysText);
        Assert.Equal("", Row(vm, 6).DetailText);
    }

    [Fact]
    public void Updates_WhenTheSourceChanges_AndUnsubscribesOnDispose()
    {
        var source = new FakeSource();
        var vm = Vm(source);
        Assert.Equal(1, source.Subscribers);
        source.Snapshot = Snapshot(true, new DungeonProgress(7, 2, 2, 2, 0, 2, 0));
        source.Raise();
        Assert.Equal("2", Row(vm, 7).KeysText);
        vm.Dispose();
        Assert.Equal(0, source.Subscribers);
    }
}
