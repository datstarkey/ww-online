using WWOnline.Native;
using Xunit;

namespace WWOnline.Tests.Native;

public sealed class NativeSettingsFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-native-ini-" + Guid.NewGuid().ToString("N"));
    private string IniPath => Path.Combine(_dir, "settings.ini");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private void Write(params string[] lines)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllLines(IniPath, lines);
    }

    [Fact]
    public void Get_ReadsAsTheGameDoes()
    {
        Write("# comment",
            "COS_FB_SCALE@handheld = 1.5",
            "COS_FB_SCALE=2.0",
            "COS_DOF=1",
            "COS_DOF=0",
            "COS_SSAA@docked=2",
            "",
            "[dev]",
            "COS_DOF=1",
            "COS_TRACE=1");
        var file = NativeSettingsFile.Load(IniPath);

        Assert.Equal("1.5", file.Get("COS_FB_SCALE", perMode: true, "handheld"));
        Assert.Equal("2.0", file.Get("COS_FB_SCALE", perMode: true, "docked"));
        Assert.Equal("0", file.Get("COS_DOF", perMode: false, "handheld")); // the last line; [dev] is not settings
        Assert.Null(file.Get("COS_SSAA", perMode: true, "handheld"));     // only the other mode's: the default
        Assert.Equal("2", file.Get("COS_SSAA", perMode: false, "handheld"));
        Assert.Null(file.Get("COS_TRACE", perMode: false, "handheld"));
        Assert.Null(file.Get("COS_MSAA", perMode: false, "handheld"));
    }

    [Fact]
    public void Set_EditsOneLineAndKeepsTheRest()
    {
        Write("# comment",
            "COS_DOF=1",
            "COS_QOL_SAIL=0",
            "COS_DOF=1",
            "",
            "[dev]",
            "COS_TRACE=1");
        var file = NativeSettingsFile.Load(IniPath);

        file.Set("COS_DOF", perMode: false, "handheld", "0");
        file.Set("COS_FB_SCALE", perMode: true, "handheld", "1.5");
        file.Set("COS_BIND_KEY_A", perMode: false, "handheld", "Space");
        file.Save();

        Assert.Equal(
            ["# comment", "COS_DOF=0", "COS_QOL_SAIL=0", "COS_FB_SCALE@handheld=1.5", "COS_BIND_KEY_A=Space", "", "[dev]", "COS_TRACE=1"],
            File.ReadAllLines(IniPath));
        Assert.False(File.Exists(IniPath + ".tmp"));
        Assert.Equal("1.5", NativeSettingsFile.Load(IniPath).Get("COS_FB_SCALE", perMode: true, "handheld"));
    }

    [Fact]
    public void Set_CreatesTheFile()
    {
        var file = NativeSettingsFile.Load(IniPath);
        Assert.Null(file.Get("COS_DOF", perMode: false, "handheld"));

        file.Set("COS_DOF", perMode: false, "handheld", "0");
        file.Save();

        Assert.Equal(["COS_DOF=0"], File.ReadAllLines(IniPath));
    }
}
