namespace WWOnline.Services;

public record PuppetSpawnCounters
{
    public uint MagicDetected { get; init; }
    public uint CreateAttempts { get; init; }
    public uint CreateSuccesses { get; init; }
    public uint CreateFailures { get; init; }
    public uint LastDesired { get; init; }
    public uint LastPid { get; init; }
    public uint DeleteIssued { get; init; }

    // Scratch BSS diagnostic — what C# wrote to the header's numActive vs what
    // the hook reads. If ClientLastWroteActive != LastDesired, something is
    // stomping the header between our write and the hook's read.
    public uint ClientLastWroteActive { get; init; }
    public uint ClientWriteCounter { get; init; }
    public uint UnstableBranchHits { get; init; }
    public uint StabilityBits { get; init; }   // bit0=stable, bit1=sceneChanged, bit2=linkAlive
    public uint Slot0VisibilityGates { get; init; } // bit0=pid set, bit1=puppet in dict, bit2=fresh, bit3=stage!="", bit4=stage match, bit5=room match
    public uint UpdateRemotePuppetCalls { get; init; }
    public uint WipeAllCalls { get; init; }

    public string StabilityFlags
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((StabilityBits & 1) != 0) parts.Add("stable");
            if ((StabilityBits & 2) != 0) parts.Add("sceneChanged");
            if ((StabilityBits & 4) != 0) parts.Add("linkAlive");
            return parts.Count == 0 ? "none" : string.Join("+", parts);
        }
    }

    public string Slot0GateFlags
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if ((Slot0VisibilityGates & 0x01) != 0) parts.Add("pid");
            if ((Slot0VisibilityGates & 0x02) != 0) parts.Add("dict");
            if ((Slot0VisibilityGates & 0x04) != 0) parts.Add("fresh");
            if ((Slot0VisibilityGates & 0x08) != 0) parts.Add("stage!empty");
            if ((Slot0VisibilityGates & 0x10) != 0) parts.Add("stage==");
            if ((Slot0VisibilityGates & 0x20) != 0) parts.Add("room==");
            return parts.Count == 0 ? "none" : string.Join("+", parts);
        }
    }

    public string Summary =>
        $"magic {MagicDetected}   desired {LastDesired} (C# wrote {ClientLastWroteActive})   " +
        $"create {CreateSuccesses}/{CreateAttempts}" +
        (CreateFailures > 0 ? $" (fail {CreateFailures})" : "") +
        $"   delete {DeleteIssued}   hdrWrites {ClientWriteCounter}   " +
        $"unstable {UnstableBranchHits} [{StabilityFlags}]   slot0 [{Slot0GateFlags}]   " +
        $"updates {UpdateRemotePuppetCalls} wipes {WipeAllCalls}";
}
