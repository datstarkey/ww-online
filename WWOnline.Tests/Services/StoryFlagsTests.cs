using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

public class StoryFlagsTests
{
    private const ushort MetKorl = 0x0F80;              // Story, risky (load spawn)
    private const ushort CourtyardCutscene = 0x3804;    // Story, risky (boat lockout)
    private const ushort CutsceneFf1 = 0x0004;          // CutsceneSeen
    private const ushort TradedToday = 0x1304;          // LocalOnly (daily reset)
    private const ushort EndlessNight = 0x0A02;         // LocalOnly
    private const ushort RiskyUnknown = 0x3E10;         // Unknown, risky: excluded

    private static StoryFlags With(params ushort[] ids)
    {
        var f = new StoryFlags();
        foreach (var id in ids) f.Bits[id >> 8] |= (byte)(id & 0xFF);
        return f;
    }

    [Fact]
    public void SyncMask_CoversOnlyTheBitFlagBytes_AndMatchesTheCatalog()
    {
        Assert.Equal(0x42, StoryFlags.ByteCount);
        Assert.Equal(StoryFlags.ByteCount, StoryFlags.SyncMask.Length);
        Assert.All(EventFlagCatalog.Flags, f => Assert.True(f.ByteIndex < StoryFlags.ByteCount));

        var full = EventFlagCatalog.GetFullSyncMask();
        Assert.Equal(full[..StoryFlags.ByteCount], StoryFlags.SyncMask.ToArray());
        Assert.All(full[StoryFlags.ByteCount..], b => Assert.Equal(0, b)); // registers never synced
    }

    [Theory]
    [InlineData(MetKorl, true)]
    [InlineData(CourtyardCutscene, true)]
    [InlineData(CutsceneFf1, true)]
    [InlineData(TradedToday, false)]
    [InlineData(EndlessNight, false)]
    [InlineData(RiskyUnknown, false)]
    public void SyncMask_IncludesStoryAndRiskyStory_ButNeverLocalOnlyOrRiskyUnknown(ushort id, bool synced)
    {
        Assert.Equal(synced, (StoryFlags.SyncMask[id >> 8] & (id & 0xFF)) != 0);
    }

    [Fact]
    public void FromEventBytes_DropsUnsyncableBits()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        Array.Fill(raw, (byte)0xFF);
        var f = StoryFlags.FromEventBytes(raw);

        Assert.True(f.Has(MetKorl));
        Assert.False(f.Has(TradedToday));
        Assert.False(f.Has(EndlessNight));
        Assert.False(f.Has(RiskyUnknown));
        Assert.Equal(StoryFlags.SyncMask.ToArray(), f.Bits);
    }

    [Fact]
    public void MergeFrom_OrsSyncableBits_AndReportsChange()
    {
        var a = With(MetKorl);
        Assert.True(a.MergeFrom(With(CutsceneFf1)));
        Assert.True(a.Has(MetKorl));
        Assert.True(a.Has(CutsceneFf1));
        Assert.Equal(2, a.BitCount);
    }

    [Fact]
    public void MergeFrom_SubsetIsNotAChange()
    {
        var a = With(MetKorl, CutsceneFf1);
        Assert.False(a.MergeFrom(With(MetKorl)));
        Assert.False(a.MergeFrom(new StoryFlags()));
    }

    [Fact]
    public void MergeFrom_IgnoresLocalOnlyBitsFromAnUnnormalizedPayload()
    {
        var a = new StoryFlags();
        Assert.False(a.MergeFrom(With(TradedToday, EndlessNight, RiskyUnknown)));
        Assert.True(a.IsEmpty);
    }

    [Fact]
    public void Normalize_StripsUnsyncableBits()
    {
        var f = With(MetKorl, TradedToday).Normalize();
        Assert.True(f.Has(MetKorl));
        Assert.False(f.Has(TradedToday));
        Assert.Equal(1, f.BitCount);
    }

    [Fact]
    public void Except()
    {
        var a = With(MetKorl, CutsceneFf1, CourtyardCutscene);
        var b = With(CutsceneFf1);

        var d = a.Except(b);
        Assert.Equal(2, d.BitCount);
        Assert.False(d.Has(CutsceneFf1));
        Assert.True(b.Except(a).IsEmpty);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var a = With(MetKorl);
        var c = a.Clone();
        c.MergeFrom(With(CutsceneFf1));
        Assert.Equal(1, a.BitCount);
        Assert.Equal(2, c.BitCount);
    }

    [Fact]
    public void IsValid_RequiresExactLength()
    {
        Assert.True(new StoryFlags().IsValid());
        Assert.False(new StoryFlags { Bits = new byte[StoryFlags.ByteCount - 1] }.IsValid());
        Assert.False(new StoryFlags { Bits = new byte[EventFlagCatalog.EventBitfieldSize] }.IsValid());
        Assert.False(new StoryFlags { Bits = null! }.IsValid());
    }

    [Fact]
    public void Describe_NamesFlags_AndTruncatesLongLists()
    {
        Assert.Equal("(none)", new StoryFlags().Describe());
        Assert.Equal("UNK_0004, MET_KORL", With(CutsceneFf1, MetKorl).Describe());

        var all = new StoryFlags { Bits = StoryFlags.SyncMask.ToArray() };
        var text = all.Describe(max: 3);
        Assert.EndsWith($", +{all.BitCount - 3} more", text);
    }

    [Fact]
    public void RiskyFlags_AreTheSetRiskyOnes()
    {
        var risky = With(MetKorl, CourtyardCutscene, CutsceneFf1).RiskyFlags().Select(f => f.Id).ToList();
        Assert.Equal(new List<ushort> { MetKorl, CourtyardCutscene }, risky);
    }

    [Fact]
    public void EveryRiskyFlag_HasARiskEffect_AndViceVersa()
    {
        var risky = EventFlagCatalog.Flags.Where(f => f.Risky).Select(f => f.Id).OrderBy(i => i).ToList();
        Assert.Equal(risky, EventFlagCatalog.RiskEffects.Keys.OrderBy(i => i).ToList());
        Assert.Contains("respawn", EventFlagCatalog.RiskEffect(MetKorl));
        Assert.Contains("King of Red Lions", EventFlagCatalog.RiskEffect(CourtyardCutscene));
        Assert.Null(EventFlagCatalog.RiskEffect(CutsceneFf1));
    }

    [Fact]
    public void AllSyncedRiskyFlags_AreStory()
    {
        var syncedRisky = new StoryFlags { Bits = StoryFlags.SyncMask.ToArray() }.RiskyFlags().ToList();
        Assert.Equal(6, syncedRisky.Count);
        Assert.All(syncedRisky, f => Assert.Equal(EventFlagCategory.Story, f.Category));
    }
}

public class RoomPresetTests
{
    [Fact]
    public void Defaults_AreFullSync()
    {
        Assert.Equal(RoomPreset.FullSync, new RoomSettings().MatchingPreset());
    }

    [Fact]
    public void ApplyPreset_SetsEveryRule()
    {
        var coop = new RoomSettings().ApplyPreset(RoomPreset.Coop);
        Assert.False(coop.SharedWallet || coop.SharedWorld || coop.SharedItems || coop.SharedStory);
        Assert.True(coop.SharedProjectiles); // Co-op still shows each other's projectiles
        Assert.Equal(RoomPreset.Coop, coop.MatchingPreset());

        var full = coop.ApplyPreset(RoomPreset.FullSync);
        Assert.True(full.SharedWallet && full.SharedWorld && full.SharedItems && full.SharedStory && full.SharedProjectiles);
        Assert.Equal(RoomPreset.FullSync, full.MatchingPreset());
    }

    [Fact]
    public void Presets_TurnProjectilesBackOn()
    {
        var s = new RoomSettings { SharedProjectiles = false }.ApplyPreset(RoomPreset.Coop);
        Assert.True(s.SharedProjectiles);
        s.SharedProjectiles = false;
        Assert.True(s.ApplyPreset(RoomPreset.FullSync).SharedProjectiles);
    }

    [Fact]
    public void ProjectilesOff_IsCustom_ForBothPresetsRules()
    {
        Assert.Equal(RoomPreset.Custom, new RoomSettings { SharedProjectiles = false }.MatchingPreset());
        var coopNoProjectiles = new RoomSettings().ApplyPreset(RoomPreset.Coop);
        coopNoProjectiles.SharedProjectiles = false;
        Assert.Equal(RoomPreset.Custom, coopNoProjectiles.MatchingPreset());
    }

    [Fact]
    public void RulesSummary_NamesTheProjectilesRule()
    {
        Assert.Contains("other players' projectiles OFF", new RoomSettings { SharedProjectiles = false }.RulesSummary());
        Assert.Contains("other players' projectiles ON", new RoomSettings().RulesSummary());
    }

    [Fact]
    public void MixedRules_AreCustom_AndCustomChangesNothing()
    {
        var s = new RoomSettings { SharedStory = false };
        Assert.Equal(RoomPreset.Custom, s.MatchingPreset());
        s.ApplyPreset(RoomPreset.Custom);
        Assert.True(s.SharedWallet);
        Assert.False(s.SharedStory);
    }
}
