using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.Tests.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public class RoomFlagsViewModelTests
{
    private const ushort MetKorl = 0x0F80;   // Story, risky, shared
    private const ushort RodeKorl = 0x2A08;  // LocalOnly: never synced
    private const ushort Unk0080 = 0x0080;   // Unknown, no description

    private sealed class FakeRoom : IStoryRoom
    {
        public bool IsConnected { get; set; }
        public bool SharedStory { get; set; }
        public bool IsOwner { get; set; }
        public StoryFlags? RoomFlags { get; set; }
        public event Action? Changed;
        public void Raise() => Changed?.Invoke();
    }

    private static FakeRoom RoomMode(bool owner) => new() { IsConnected = true, SharedStory = true, IsOwner = owner };
    private static FakeRoom LocalMode() => new() { IsConnected = true, SharedStory = false };

    /// <summary>A game in play on the sea: Link exists, no stage change pending.</summary>
    private static FakeDolphin GameInPlay()
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80400000);
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, (byte)'s', (byte)'e', (byte)'a', 0);
        return d;
    }

    private static RoomFlagsViewModel Vm(FakeDolphin dolphin, FakeRoom room) =>
        new(dolphin, room, a => a(), startPolling: false);

    /// <summary>Poll until the scene gate (2 stable ticks after the stage is first seen) lets reads through.</summary>
    private static void PollUntilStable(RoomFlagsViewModel vm)
    {
        for (int i = 0; i < 3; i++) vm.Poll();
    }

    private static uint FlagAddr(ushort id) => EventFlagCatalog.EventBitfieldAddress + (uint)(id >> 8);
    private static bool IsSetInGame(FakeDolphin d, ushort id) => (d.Get(FlagAddr(id)) & (id & 0xFF)) != 0;
    private static void SetInGame(FakeDolphin d, ushort id) => d.Set(FlagAddr(id), (byte)(d.Get(FlagAddr(id)) | (id & 0xFF)));

    private static StoryFlagRow Row(RoomFlagsViewModel vm, ushort id) => vm.AllRows.Single(r => r.Info.Id == id);

    // ═══ Owner / non-owner gating ═══

    [Fact]
    public void RoomMode_NonOwner_IsViewOnly_AndCannotWrite()
    {
        var game = GameInPlay();
        using var vm = Vm(game, RoomMode(owner: false));
        PollUntilStable(vm);

        Assert.True(vm.IsRoomMode);
        Assert.False(vm.Edit.CanEdit);
        Assert.True(vm.ShowViewOnly);
        Assert.False(vm.ShowEditButton);

        vm.StartEditCommand.Execute(null);
        Assert.False(vm.IsEditing);

        vm.ToggleFlagCommand.Execute(Row(vm, MetKorl));
        Assert.False(IsSetInGame(game, MetKorl));
    }

    [Fact]
    public void RoomMode_Owner_CanEdit_AndTickingASharedFlagSetsItInTheOwnersGame()
    {
        var game = GameInPlay();
        using var vm = Vm(game, RoomMode(owner: true));
        PollUntilStable(vm);

        Assert.True(vm.ShowEditButton);
        vm.StartEditCommand.Execute(null);
        Assert.True(vm.IsEditing);

        var row = Row(vm, MetKorl);
        Assert.True(row.CanToggle);
        vm.ToggleFlagCommand.Execute(row);

        Assert.True(IsSetInGame(game, MetKorl)); // StorySyncService merges it up from here
        Assert.True(row.IsSet);
        Assert.Contains("room", vm.StatusMessage);
        Assert.Contains("Warning", vm.StatusMessage); // MET_KORL is risky
    }

    [Fact]
    public void LosingOwnership_DropsEditMode()
    {
        var room = RoomMode(owner: true);
        using var vm = Vm(GameInPlay(), room);
        vm.StartEditCommand.Execute(null);
        Assert.True(vm.IsEditing);

        room.IsOwner = false;
        room.Raise();

        Assert.False(vm.IsEditing);
        Assert.False(vm.Edit.CanEdit);
    }

    [Fact]
    public void Disconnect_DropsEditMode_ThenAnyoneMayEditTheirOwnGame()
    {
        var room = RoomMode(owner: true);
        using var vm = Vm(GameInPlay(), room);
        vm.StartEditCommand.Execute(null);

        room.IsConnected = false;
        room.Raise();

        Assert.False(vm.IsEditing);
        Assert.False(vm.IsRoomMode);
        Assert.True(vm.Edit.CanEdit); // offline: your game only
    }

    [Fact]
    public void SharedStoryTurnedOff_DropsEditMode()
    {
        var room = RoomMode(owner: true);
        using var vm = Vm(GameInPlay(), room);
        vm.StartEditCommand.Execute(null);

        room.SharedStory = false;
        room.Raise();

        Assert.False(vm.IsEditing);
        Assert.False(vm.IsRoomMode);
    }

    [Fact]
    public void SharedStoryTurnedOn_NonOwnerEditBlocked_EvenBeforeThePageRefreshes()
    {
        // Non-owner editing their own game; the owner turns Shared story on. The write must be refused
        // on the live rule, not the page's cached mode (the Changed refresh is posted to the UI thread).
        var game = GameInPlay();
        var room = LocalMode();
        using var vm = Vm(game, room);
        PollUntilStable(vm);
        vm.StartEditCommand.Execute(null);
        Assert.True(vm.IsEditing);

        room.SharedStory = true; // no Raise(): the page hasn't seen it yet
        vm.ToggleFlagCommand.Execute(Row(vm, MetKorl));

        Assert.False(IsSetInGame(game, MetKorl));
    }

    // ═══ Shared vs local mode ═══

    [Fact]
    public void LocalMode_NonOwner_CanTickAndUntickTheirOwnGame()
    {
        var game = GameInPlay();
        using var vm = Vm(game, LocalMode());
        PollUntilStable(vm);

        Assert.False(vm.IsRoomMode);
        Assert.Equal("Your game only", vm.SyncedText);
        Assert.True(vm.Edit.CanEdit);
        vm.StartEditCommand.Execute(null);

        var row = Row(vm, MetKorl);
        vm.ToggleFlagCommand.Execute(row);
        Assert.True(IsSetInGame(game, MetKorl));

        vm.ToggleFlagCommand.Execute(row); // shared flag, but the room doesn't own the story: untick is allowed
        Assert.False(IsSetInGame(game, MetKorl));
        Assert.False(row.IsSet);
    }

    [Fact]
    public void RoomMode_ShowsTheRoomsSharedFlags_LocalModeDoesNot()
    {
        var room = RoomMode(owner: false);
        var roomFlags = new StoryFlags();
        roomFlags.Bits[MetKorl >> 8] |= MetKorl & 0xFF;
        room.RoomFlags = roomFlags;
        using var vm = Vm(GameInPlay(), room);
        PollUntilStable(vm);

        Assert.True(Row(vm, MetKorl).IsSet);
        Assert.True(vm.HasData);

        room.SharedStory = false;
        room.Raise();
        vm.Poll();
        Assert.False(Row(vm, MetKorl).IsSet);
    }

    [Fact]
    public void RoomMode_CannotUntickASharedFlag()
    {
        var game = GameInPlay();
        SetInGame(game, MetKorl);
        using var vm = Vm(game, RoomMode(owner: true));
        PollUntilStable(vm);
        vm.StartEditCommand.Execute(null);

        var row = Row(vm, MetKorl);
        Assert.True(row.IsSet);
        Assert.True(row.IsLockedByRoom);
        Assert.True(row.ShowLock);
        Assert.False(row.CanToggle);

        vm.ToggleFlagCommand.Execute(row);

        Assert.True(IsSetInGame(game, MetKorl));
        Assert.Equal(RoomFlagsViewModel.RoomKeepsFlagsNote, vm.StatusMessage);
    }

    [Fact]
    public void RoomMode_OwnerCanUntickAThisGameOnlyFlag()
    {
        var game = GameInPlay();
        SetInGame(game, RodeKorl);
        using var vm = Vm(game, RoomMode(owner: true));
        PollUntilStable(vm);
        vm.StartEditCommand.Execute(null);

        var row = Row(vm, RodeKorl);
        Assert.False(row.IsLockedByRoom);
        Assert.True(row.CanToggle);
        vm.ToggleFlagCommand.Execute(row);

        Assert.False(IsSetInGame(game, RodeKorl));
        Assert.Contains("never reaches other players", vm.StatusMessage);
    }

    // ═══ Scene gate / edit lock ═══

    [Fact]
    public void Writes_AreBlocked_WhenTheSceneIsUnstable()
    {
        var game = GameInPlay();
        using var vm = Vm(game, LocalMode());
        PollUntilStable(vm);
        vm.StartEditCommand.Execute(null);

        game.Set(GameMemoryAddresses.WorldFlags.NextStageEnable, 1); // stage change pending
        vm.Poll();
        vm.ToggleFlagCommand.Execute(Row(vm, MetKorl));

        Assert.False(IsSetInGame(game, MetKorl));
        Assert.Contains("isn't in play", vm.StatusMessage);
    }

    [Fact]
    public void Writes_AreBlocked_BeforeTheSceneHasSettled()
    {
        var game = GameInPlay();
        using var vm = Vm(game, LocalMode());
        vm.Poll(); // first sight of the stage: not stable yet
        vm.StartEditCommand.Execute(null);
        vm.ToggleFlagCommand.Execute(Row(vm, MetKorl));

        Assert.False(IsSetInGame(game, MetKorl));
    }

    [Fact]
    public void Writes_AreBlocked_OutsideEditMode()
    {
        var game = GameInPlay();
        using var vm = Vm(game, LocalMode());
        PollUntilStable(vm);

        vm.ToggleFlagCommand.Execute(Row(vm, MetKorl));

        Assert.False(IsSetInGame(game, MetKorl));
        Assert.False(Row(vm, MetKorl).CanToggle);
    }

    // ═══ Sorting / search / unknown toggle ═══

    [Fact]
    public void List_IsNamedFlagsSortedByDescription_UnknownHiddenByDefault()
    {
        using var vm = Vm(GameInPlay(), LocalMode());

        Assert.Equal(EventFlagCatalog.Flags.Count, vm.TotalCount);
        Assert.True(vm.UnknownCount > 0);
        Assert.All(vm.VisibleRows, r => Assert.True(r.IsNamed));
        Assert.Equal(vm.TotalCount - vm.UnknownCount, vm.VisibleRows.Count);
        var keys = vm.VisibleRows.Select(r => r.SortKey).ToList();
        Assert.Equal(keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(), keys);
        Assert.DoesNotContain(vm.VisibleRows, r => r.Info.Id == Unk0080);
    }

    [Fact]
    public void ShowUnknown_AppendsUnknownFlagsSortedByCode()
    {
        using var vm = Vm(GameInPlay(), LocalMode());
        vm.ShowUnknown = true;

        Assert.Equal(vm.TotalCount, vm.VisibleRows.Count);
        var tail = vm.VisibleRows.Skip(vm.TotalCount - vm.UnknownCount).ToList();
        Assert.All(tail, r => Assert.False(r.IsNamed));
        Assert.Equal(tail.Select(r => r.Info.Id).OrderBy(id => id).ToList(), tail.Select(r => r.Info.Id).ToList());
        Assert.Contains(tail, r => r.Info.Id == Unk0080);
    }

    [Theory]
    [InlineData("RODE_KORL")]
    [InlineData("rode korl")]
    [InlineData("2a08")]
    [InlineData("0x2A08")]
    [InlineData("first time")]
    public void Search_MatchesDecompName_Description_AndHexCode(string query)
    {
        using var vm = Vm(GameInPlay(), LocalMode());
        vm.Search = query;
        Assert.Contains(vm.VisibleRows, r => r.Info.Id == RodeKorl);
    }

    [Fact]
    public void Search_FindsUnknownFlags_EvenWithTheToggleOff()
    {
        using var vm = Vm(GameInPlay(), LocalMode());
        vm.Search = "UNK_0080";
        Assert.Single(vm.VisibleRows);
        Assert.Equal(Unk0080, vm.VisibleRows[0].Info.Id);

        vm.Search = "no flag is called this";
        Assert.True(vm.HasNoMatches);

        vm.ClearSearchCommand.Execute(null);
        Assert.Equal(vm.TotalCount - vm.UnknownCount, vm.VisibleRows.Count);
    }

    // ═══ Tags (catalog class + scope) ═══

    [Fact]
    public void Tags_ShowTheCatalogClass_AndWhichFlagsNeverReachOtherPlayers()
    {
        using var vm = Vm(GameInPlay(), LocalMode());

        var rode = Row(vm, RodeKorl);
        Assert.Equal("RODE_KORL", rode.Name);
        Assert.NotNull(rode.Description);
        Assert.Equal("Local only", rode.ClassText);
        Assert.False(rode.IsShared);
        Assert.Equal("This game only", rode.ScopeText);
        Assert.Contains("never reaches other players", rode.Tooltip);

        var met = Row(vm, MetKorl);
        Assert.Equal("Story", met.ClassText);
        Assert.True(met.IsShared);
        Assert.True(met.IsRisky);
        Assert.Equal(EventFlagCatalog.RiskEffect(MetKorl), met.RiskText);
        Assert.Contains("Risky", met.Tooltip);
    }

    [Fact]
    public void SetCount_CountsEverySetFlag()
    {
        var game = GameInPlay();
        SetInGame(game, MetKorl);
        SetInGame(game, RodeKorl);
        using var vm = Vm(game, LocalMode());
        Assert.False(vm.HasData);

        PollUntilStable(vm);

        Assert.Equal(2, vm.SetCount);
        Assert.Equal($"2 of {vm.TotalCount} set", vm.SetCountText);
    }

    // ═══ Event registers (this game only) ═══

    [Fact]
    public void SetRegister_WritesOnlyTheRegisterBits()
    {
        var game = GameInPlay();
        using var vm = Vm(game, LocalMode());
        var reg = vm.Registers.Single(r => r.Info.Id == 0x7A03); // letter state, mask 0x03
        uint addr = EventFlagCatalog.EventBitfieldAddress + 0x7A;
        game.Set(addr, 0xF0);
        PollUntilStable(vm);
        vm.StartEditCommand.Execute(null);

        reg.EditText = "5"; // out of range for a 2-bit register
        vm.SetRegisterCommand.Execute(reg);
        Assert.Equal(0xF0, game.Get(addr));

        reg.EditText = "2";
        vm.SetRegisterCommand.Execute(reg);
        Assert.Equal(0xF2, game.Get(addr));
        Assert.Equal(2, reg.Value);
    }
}
