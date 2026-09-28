namespace WWOnline.Services;

public record PuppetDiagnosticRow
{
    public string PlayerId { get; init; } = "";
    public int Slot { get; init; }
    public string PuppetStage { get; init; } = "";
    public byte PuppetRoom { get; init; }
    public string LocalStage { get; init; } = "";
    public byte LocalRoom { get; init; }
    public bool IsVisible { get; init; }
    public bool HasPuppetData { get; init; }
    public DateTime? LastPuppetReceived { get; init; }

    public float PuppetX { get; init; }
    public float PuppetY { get; init; }
    public float PuppetZ { get; init; }
    public float LocalX { get; init; }
    public float LocalY { get; init; }
    public float LocalZ { get; init; }

    public float DistanceToLocal
    {
        get
        {
            var dx = PuppetX - LocalX;
            var dy = PuppetY - LocalY;
            var dz = PuppetZ - LocalZ;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public string PositionText => $"({PuppetX:F0}, {PuppetY:F0}, {PuppetZ:F0})";
    public string LocalPositionText => $"({LocalX:F0}, {LocalY:F0}, {LocalZ:F0})";
    public string DistanceText => $"{DistanceToLocal:F0} u";

    public string AgeText
    {
        get
        {
            if (LastPuppetReceived is not DateTime t) return "—";
            var ms = (DateTime.UtcNow - t).TotalMilliseconds;
            return ms > 60_000 ? ">60s" : $"{(int)ms} ms";
        }
    }

    public string ShortId => PlayerId.Length > 8 ? PlayerId[..8] : PlayerId;
}
