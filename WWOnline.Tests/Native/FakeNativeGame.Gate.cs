using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The gate half of tww.h: a settable <see cref="Gate"/> state, and the warps asked for.</summary>
internal sealed partial class FakeNativeGame : ITwwGateApi
{
    public TwwGateState Gate;
    public List<(float X, float Y, float Z, short AngleY)> Warps { get; } = [];
    public List<(string Stage, short Point, sbyte Room, sbyte Layer)> StageChanges { get; } = [];

    public TwwGateState GetGateState() => Gate;

    public bool WarpLocal(float x, float y, float z, short angleY)
    {
        if (Gate.Link == 0 || Gate.NextStage != 0) return false;
        Warps.Add((x, y, z, angleY));
        if (Snapshot is { } snapshot)
        {
            var local = snapshot.Local;
            local.Pos[0] = x;
            local.Pos[1] = y;
            local.Pos[2] = z;
            Snapshot = snapshot with { Local = local };
        }
        return true;
    }

    public bool ChangeStage(string stage, short point, sbyte room, sbyte layer)
    {
        if (Gate.Link == 0 || Gate.NextStage != 0) return false;
        StageChanges.Add((stage, point, room, layer));
        Gate.NextStage = 1;
        return true;
    }

    /// <summary>Link in play at <paramref name="stage"/>, entered at point 0 of <paramref name="room"/>, no event, menu closed.</summary>
    public static TwwGateState InPlay(string stage, sbyte room) => new()
    {
        Stage = TwwStageName.From(stage),
        StartRoom = room,
        StartLayer = -1,
        Room = room,
        Link = 1,
    };
}
