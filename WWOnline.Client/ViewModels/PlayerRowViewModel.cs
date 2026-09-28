using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WWOnline.Data;
using WWOnline.Services;

namespace WWOnline.ViewModels;

public partial class PlayerRowViewModel : ObservableObject
{
    public string ConnectionId { get; }
    public bool IsLocal { get; }

    [ObservableProperty] private string _playerName = "";
    [ObservableProperty] private bool _hasGameState;
    [ObservableProperty] private int _currentHealth;
    [ObservableProperty] private int _maxHealth;
    [ObservableProperty] private int _rupees;
    [ObservableProperty] private int _sector;
    [ObservableProperty] private string _stageName = "";
    [ObservableProperty] private bool _isInGame;
    [ObservableProperty] private DateTime _lastUpdate = DateTime.UtcNow;

    /// <summary>The room owner's row gets a crown (set by the Room page from the room rules).</summary>
    [ObservableProperty] private bool _isOwner;

    /// <summary>Avatar circle: the player's tunic colour (the default tunic green until known).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvatarForeground))]
    private IBrush _tunicBrush = TunicColors.DefaultBrush;

    public IBrush AvatarForeground => TunicColors.ForegroundFor(TunicBrush);

    public string LocationText => IsInGame
        ? (string.IsNullOrWhiteSpace(StageName) ? $"Sector {Sector}" : $"{StageName} · Sector {Sector}")
        : "Title Menu";

    /// <summary>Friendly stage name for the Players card ("The Great Sea", "Dragon Roost Cavern").</summary>
    public string StageText => !HasGameState ? "Waiting for game"
        : !IsInGame ? "Title menu"
        : string.IsNullOrWhiteSpace(StageName) ? $"Sector {Sector}"
        : StageIDs.GetStageName(StageName.TrimEnd('\0'));

    /// <summary>Two-letter avatar label: "Player1" → "P1", "Jake Smith" → "JS".</summary>
    public string Initials => TunicColors.InitialsOf(PlayerName);

    // WW health is in quarter-hearts (4 = one heart); show full hearts with a half marker.
    public string HeartsText => $"{CurrentHealth / 4}{((CurrentHealth % 4) >= 2 ? "½" : "")}";

    partial void OnStageNameChanged(string value) { OnPropertyChanged(nameof(LocationText)); OnPropertyChanged(nameof(StageText)); }
    partial void OnSectorChanged(int value) { OnPropertyChanged(nameof(LocationText)); OnPropertyChanged(nameof(StageText)); }
    partial void OnIsInGameChanged(bool value) { OnPropertyChanged(nameof(LocationText)); OnPropertyChanged(nameof(StageText)); }
    partial void OnHasGameStateChanged(bool value) => OnPropertyChanged(nameof(StageText));
    partial void OnPlayerNameChanged(string value) => OnPropertyChanged(nameof(Initials));
    partial void OnCurrentHealthChanged(int value) => OnPropertyChanged(nameof(HeartsText));

    public string ShortId => ConnectionId.Length > 8 ? ConnectionId[..8] : ConnectionId;

    public PlayerRowViewModel(string connectionId, string playerName, bool isLocal)
    {
        ConnectionId = connectionId;
        IsLocal = isLocal;
        _playerName = playerName;
    }
}

/// <summary>Tunic colour → avatar brush (the Appearance page's presets; the vanilla tunic shows as its display green).</summary>
public static class TunicColors
{
    /// <summary>What the Default preset shows on the Appearance page (AppearanceViewModel.ColorPresets[0]).</summary>
    public const string DefaultHex = "#5AB24A";

    public static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse(DefaultHex));

    private static readonly IBrush DarkText = new SolidColorBrush(Color.Parse("#06120A"));
    private static readonly IBrush LightText = new SolidColorBrush(Colors.White);

    public static IBrush FromHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || !Color.TryParse(hex, out var c)) return DefaultBrush;
        return new SolidColorBrush(c);
    }

    public static IBrush FromRgb(byte r, byte g, byte b) =>
        LocalAppearance.IsDefaultColor(r, g, b) ? DefaultBrush : new SolidColorBrush(Color.FromRgb(r, g, b));

    /// <summary>Dark initials on light tunics, white on dark ones.</summary>
    public static IBrush ForegroundFor(IBrush background)
    {
        if (background is not ISolidColorBrush solid) return LightText;
        var c = solid.Color;
        double luma = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        return luma > 0.55 ? DarkText : LightText;
    }

    public static string InitialsOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        var word = parts[0];
        // "Player1" → "P1": first letter + trailing digits, else the first two letters.
        int d = word.Length;
        while (d > 1 && char.IsDigit(word[d - 1])) d--;
        if (d < word.Length && word.Length - d <= 2)
            return $"{char.ToUpperInvariant(word[0])}{word[d..]}";
        return word.Length >= 2 ? $"{char.ToUpperInvariant(word[0])}{char.ToLowerInvariant(word[1])}" : word.ToUpperInvariant();
    }
}
