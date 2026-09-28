namespace WWOnline.Data;

/// <summary>
/// Friendly labels for Wind Waker stage names (the 8-char string at CurrentStageName).
/// Every key must be a real folder under &lt;game&gt;/files/res/Stage/ — the game files are the
/// source of truth; PuppetLayoutTests-style checks can't see them, so verify by listing that
/// folder before adding. Islands are rooms of "sea", so they show as "The Great Sea".
/// Anything not listed falls back to the raw stage name.
/// </summary>
public static class StageIDs
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sea"] = "The Great Sea",
        ["sea_T"] = "Title Screen Sea",
        ["Name"] = "File Select",
        ["LinkRM"] = "Link's House",
        ["LinkUG"] = "Under Link's House",
        ["Omori"] = "Forest Haven",
        ["MajyuE"] = "Forsaken Fortress (exterior)",
        ["majroom"] = "Forsaken Fortress (interior)",
        ["ma2room"] = "Forsaken Fortress (interior)",
        ["ma3room"] = "Forsaken Fortress (interior)",
        ["Mjtower"] = "Forsaken Fortress (tower)",
        ["M_NewD2"] = "Dragon Roost Cavern",
        ["M_DragB"] = "Gohma (boss)",
        ["kindan"] = "Forbidden Woods",
        ["kinMB"] = "Forbidden Woods (miniboss)",
        ["kinBOSS"] = "Kalle Demos (boss)",
        ["Siren"] = "Tower of the Gods",
        ["SirenMB"] = "Tower of the Gods (miniboss)",
        ["SirenB"] = "Gohdan (boss)",
        ["M_Dai"] = "Earth Temple",
        ["M_DaiMB"] = "Earth Temple (miniboss)",
        ["M_DaiB"] = "Jalhalla (boss)",
        ["kaze"] = "Wind Temple",
        ["kazeMB"] = "Wind Temple (miniboss)",
        ["kazeB"] = "Molgera (boss)",
        ["Hyrule"] = "Hyrule Castle",
        ["Hyroom"] = "Hyrule Castle (interior)",
        ["kenroom"] = "Master Sword Chamber",
        ["GTower"] = "Ganon's Tower (exterior)",
        ["PShip"] = "Ghost Ship",
        ["Fairy01"] = "Fairy Fountain",
        ["Fairy02"] = "Fairy Fountain",
        ["Fairy03"] = "Fairy Fountain",
        ["Fairy04"] = "Fairy Fountain",
        ["Fairy05"] = "Fairy Fountain",
        ["Fairy06"] = "Fairy Fountain",
    };

    /// <summary>
    /// Stages that aren't gameplay: the title-screen demo (a real Link on the boat, but with an
    /// empty default save) and file select. Sync services must never read or write save data here
    /// — seeding the shared wallet from the title save wiped the owner's rupees. The injected hook
    /// skips the same two (link_draw_hook.c STAGE_SEA+'T' / STAGE_NAME_ID).
    /// </summary>
    public static bool IsNonGameplayStage(string stageId) =>
        string.Equals(stageId, "sea_T", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(stageId, "Name", StringComparison.OrdinalIgnoreCase);

    /// <summary>Human-readable name for a stage, or the raw stage name if unknown.</summary>
    public static string GetStageName(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return "Unknown";
        if (Labels.TryGetValue(stageId, out var label)) return label;
        if (stageId.StartsWith("Ganon", StringComparison.OrdinalIgnoreCase)) return $"Ganon's Tower ({stageId})";
        if (stageId.StartsWith("Cave", StringComparison.OrdinalIgnoreCase)) return $"Secret Cave ({stageId})";
        return stageId;
    }
}
