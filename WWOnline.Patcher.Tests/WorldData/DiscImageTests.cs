using System.Text;
using WWOnline.Patcher.WorldData;
using Xunit;

namespace WWOnline.Patcher.Tests.WorldData;

/// <summary>The switch table from a disc image, for the native backend (it has no extracted game).</summary>
public sealed class DiscImageTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "wwo-disc-" + Guid.NewGuid().ToString("N") + ".iso");

    public void Dispose()
    {
        try { File.Delete(_path); } catch (IOException) { }
    }

    private static void U32(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    /// <summary>A disc with res/Stage/Abc/{Stage.arc, Room1.arc} and a two-section main.dol.</summary>
    private void WriteDisc()
    {
        var disc = new byte[0x4000];
        U32(disc, 0x1C, 0xC2339F3D);
        U32(disc, 0x420, 0x2000);                      // main.dol
        U32(disc, 0x2000, 0x100);                      // text0 at +0x100, 0x20 bytes
        U32(disc, 0x2000 + 0x90, 0x20);
        U32(disc, 0x2000 + 0x1C, 0x200);               // data0 at +0x200, 0x40 bytes: the DOL ends at 0x240
        U32(disc, 0x2000 + 0xAC, 0x40);

        // root, res/, Stage/, Abc/, Stage.arc, Room1.arc, and a file beside res/
        string[] names = ["", "res", "Stage", "Abc", "Stage.arc", "Room1.arc", "opening.bnr"];
        var strings = new List<byte>();
        var nameOffsets = names.Select(n => { int o = strings.Count; strings.AddRange(Encoding.ASCII.GetBytes(n)); strings.Add(0); return o; }).ToArray();
        (bool Dir, uint A, uint B)[] entries =
        [
            (true, 0, 7), (true, 0, 6), (true, 1, 6), (true, 2, 6),
            (false, 0x3000, 4), (false, 0x3010, 3), (false, 0x3020, 1),
        ];
        int fst = 0x1000;
        for (int i = 0; i < entries.Length; i++)
        {
            U32(disc, fst + i * 12, (entries[i].Dir ? 0x01000000u : 0) | (uint)nameOffsets[i]);
            U32(disc, fst + i * 12 + 4, entries[i].A);
            U32(disc, fst + i * 12 + 8, entries[i].B);
        }
        strings.CopyTo(disc, fst + entries.Length * 12);
        U32(disc, 0x424, (uint)fst);
        U32(disc, 0x428, (uint)(entries.Length * 12 + strings.Count));
        "STAG"u8.CopyTo(disc.AsSpan(0x3000));
        "ROOM"u8.CopyTo(disc.AsSpan(0x3010));
        File.WriteAllBytes(_path, disc);
    }

    [Fact]
    public void Files_AreFoundByTheirFstPath()
    {
        WriteDisc();
        using var disc = DiscImage.Open(_path)!;

        Assert.Equal(["res/Stage/Abc"], disc.Directories("res/Stage"));
        Assert.Equal(["res/Stage/Abc/Room1.arc", "res/Stage/Abc/Stage.arc"], disc.Files("res/Stage/Abc").Order());
        Assert.Equal(["opening.bnr"], disc.Files(""));
        Assert.Equal("STAG"u8.ToArray(), disc.ReadFile("res/Stage/Abc/Stage.arc"));
        Assert.Equal("ROO"u8.ToArray(), disc.ReadFile("res/Stage/Abc/Room1.arc"));
        Assert.Null(disc.ReadFile("res/Stage/Abc/Room2.arc"));
        Assert.Equal(0x240, disc.ReadDol().Length);
    }

    [Fact]
    public void NotAGameCubeDisc_OpensAsNull()
    {
        File.WriteAllBytes(_path, new byte[0x1000]);
        Assert.Null(DiscImage.Open(_path));
        Assert.Null(DiscImage.Open(_path + ".missing"));
        Assert.Null(SwitchTableBuilder.BuildFromGame(_path));
    }

    [DiscFact]
    public void TheSwitchTable_BuildsFromTheDisc()
    {
        var table = SwitchTableBuilder.BuildFromGame(Environment.GetEnvironmentVariable(DiscFactAttribute.Variable)!)!;

        Assert.Equal(SwitchTableBuilder.RulesVersion, table.Rules);
        Assert.True(table.StageCount > 100, $"{table.StageCount} stages");
        Assert.NotEmpty(table.Dan);
        Assert.NotEmpty(table.Zone);
        Assert.NotEmpty(table.MemoryExcluded); // push blocks
        if (TestRepo.VanillaGamePath() is { } game) // the same table as from the extracted game
            Assert.Equal(Json(SwitchTableBuilder.BuildFromGame(game)!), Json(table));
    }

    private static string Json(SwitchTable t) => System.Text.Json.JsonSerializer.Serialize(t);
}

/// <summary>A [Fact] reported as skipped unless WWO_DISC names a GameCube disc image (Nintendo files aren't in the repo).</summary>
internal sealed class DiscFactAttribute : FactAttribute
{
    public const string Variable = "WWO_DISC";

    public DiscFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"No disc image: set {Variable}";
    }
}
