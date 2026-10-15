using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using WWOnline.Native;

namespace WWOnline.ViewModels;

/// <summary>One of the game's settings (a row on Settings' game tabs): a toggle ("bool") or a list (a choice, a key or button binding).</summary>
public partial class GameSettingItemViewModel : ObservableObject
{
    private readonly GameSettingsViewModel _owner;
    private bool _updating;

    internal GameSettingItemViewModel(GameSettingsViewModel owner, GameSettingInfo info)
    {
        _owner = owner;
        _info = info;
        Choices = new ObservableCollection<GameSettingChoice>(info.Choices);
        Show(info);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditable), nameof(RestartPending), nameof(Tip))]
    private GameSettingInfo _info;

    /// <summary>The toggle's state ("1").</summary>
    [ObservableProperty] private bool _isOn;

    /// <summary>The list's selection.</summary>
    [ObservableProperty] private GameSettingChoice? _selected;

    public string Key => Info.Key;
    public string Label => Info.Label;
    public string Help => Info.Help;
    public bool IsToggle => Info.Type == "bool";
    public bool IsList => !IsToggle;
    public bool NeedsRestart => Info.Restart;
    public bool RestartPending => Info.RestartPending;
    public bool IsEditable => !Info.Locked;
    public ObservableCollection<GameSettingChoice> Choices { get; }

    /// <summary>The help, plus why it can't be changed.</summary>
    public string Tip => Info.Locked ? $"{Help} Set by an environment variable ({Key}): change it there." : Help;

    partial void OnIsOnChanged(bool value)
    {
        if (!_updating) _owner.Change(this, value ? "1" : "0");
    }

    partial void OnSelectedChanged(GameSettingChoice? value)
    {
        if (!_updating && value != null && value.Value != Info.Value) _owner.Change(this, value.Value);
    }

    /// <summary>Shows a value read back, without changing anything.</summary>
    internal void Show(GameSettingInfo info)
    {
        _updating = true;
        try
        {
            Info = info;
            // A value the list lacks (a key bound in another program's terms, a hand edit): shown as it is.
            var choice = Choices.FirstOrDefault(c => c.Value == info.Value);
            if (choice == null)
            {
                choice = new GameSettingChoice(info.Value, info.Value.Length > 0 ? info.Value : "-");
                Choices.Add(choice);
            }
            Selected = choice;
            IsOn = info.Value == "1";
        }
        finally
        {
            _updating = false;
        }
    }
}

/// <summary>One card of a tab: the tab's own settings, or the keyboard's or the controller's bindings.</summary>
public sealed record GameSettingsGroup(string Title, string Description, IReadOnlyList<GameSettingItemViewModel> Items);

/// <summary>One tab of Settings: a tab of the game's options menu (Controls holds the bindings too).</summary>
public sealed partial class GameSettingsSection(string title, IReadOnlyList<GameSettingsGroup> groups) : ObservableObject
{
    public string Title { get; } = title;
    public IReadOnlyList<GameSettingsGroup> Groups { get; } = groups;

    /// <summary>Its tab is the one showing.</summary>
    [ObservableProperty] private bool _isSelected;
}

/// <summary>
/// The native game's own options (native backend; Settings shows them as tabs): its options menu, built from its catalogue
/// (<see cref="NativeGameSettings"/>). A change applies at once while the game runs, else it is saved to the game's
/// settings file for its next start. Binding a key or button another row has swaps the two, as the game's menu does.
/// The game's calls run off the UI thread; their results come back through <c>post</c>.
/// </summary>
public partial class GameSettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GameSettingsViewModel>();

    private readonly NativeGameSettings _settings;
    private readonly Action<Action> _post;
    private readonly Dictionary<string, GameSettingItemViewModel> _items = new(StringComparer.Ordinal);
    private GameSettingsCatalog? _catalog;

    public GameSettingsViewModel(NativeGameSettings settings) : this(settings, a => Dispatcher.UIThread.Post(a))
    {
    }

    /// <param name="settings">The game's settings.</param>
    /// <param name="post">Runs an action on the UI thread (tests pass <c>a =&gt; a()</c>).</param>
    public GameSettingsViewModel(NativeGameSettings settings, Action<Action> post)
    {
        _settings = settings;
        _post = post;
        _settings.Game.RunningChanged += OnRunningChanged;
        Pending = ReloadAsync();
    }

    public ObservableCollection<GameSettingsSection> Sections { get; } = [];

    /// <summary>The section Settings shows when one of the game's tabs is picked; kept by its title across a reload.</summary>
    [ObservableProperty] private GameSettingsSection? _selectedSection;

    /// <summary>One of the game's tabs is showing on Settings.</summary>
    [ObservableProperty] private bool _isShown;

    partial void OnSelectedSectionChanged(GameSettingsSection? value) => MarkSelected();
    partial void OnIsShownChanged(bool value) => MarkSelected();

    private void MarkSelected()
    {
        foreach (var s in Sections) s.IsSelected = IsShown && s == SelectedSection;
    }

    /// <summary>The game has listed its settings (now, or at an earlier run).</summary>
    [ObservableProperty] private bool _hasCatalog;

    /// <summary>The game runs: changes apply now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isLive;

    /// <summary>The last change the game refused, or "".</summary>
    [ObservableProperty] private string _error = "";

    public string StatusText => IsLive
        ? "The game is running: changes apply now, except those marked Restart."
        : $"The game isn't running: changes are saved to {_settings.SettingsPath} and apply when it starts.";

    /// <summary>The latest load or change (tests await it).</summary>
    public Task Pending { get; private set; }

    /// <summary>Reads the settings again (the page is shown, the game started or stopped).</summary>
    public Task ReloadAsync() => Pending = RunAsync(() => _settings.Load());

    internal void Change(GameSettingItemViewModel item, string value)
    {
        if (_catalog is not { } catalog) return;
        var setting = item.Info;
        Error = "";
        // The row that has this key or button now gets this row's old one.
        var other = setting.Type == "binding" && value is not ("" or "none")
            ? _items.Values.FirstOrDefault(i => i != item && i.Info.Group == setting.Group && i.Info.Value == value)?.Info
            : null;
        Pending = RunAsync(() =>
        {
            if (other != null) _settings.Set(catalog, other, setting.Value);
            if (!_settings.Set(catalog, setting, value))
            {
                Logger.Warning("[settings] {Key}={Value} was not taken", setting.Key, value);
                _post(() => Error = $"{setting.Label}: the game didn't take “{value}”.");
            }
            return _settings.Load();
        });
    }

    private async Task RunAsync(Func<GameSettingsCatalog?> work)
    {
        GameSettingsCatalog? catalog;
        try
        {
            catalog = await Task.Run(work);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[settings] couldn't read the game's settings");
            _post(() => Error = ex.Message);
            return;
        }
        bool live = _settings.Game.IsRunning;
        _post(() => Show(catalog, live));
    }

    private void Show(GameSettingsCatalog? catalog, bool live)
    {
        _catalog = catalog;
        IsLive = live;
        HasCatalog = catalog != null;
        if (catalog == null)
        {
            Sections.Clear();
            _items.Clear();
            SelectedSection = null;
            return;
        }
        if (catalog.Settings.Count == _items.Count && catalog.Settings.All(s => _items.ContainsKey(s.Key)))
        {
            foreach (var s in catalog.Settings) _items[s.Key].Show(s);
            return;
        }
        var selected = SelectedSection?.Title;
        Sections.Clear();
        _items.Clear();
        foreach (var s in catalog.Settings) _items[s.Key] = new GameSettingItemViewModel(this, s);
        // Tabs in the game's order; within one, its own settings first, then the keyboard's and the controller's.
        foreach (var tab in catalog.Settings.GroupBy(s => s.Tab))
        {
            var groups = tab.GroupBy(s => s.Group).OrderBy(g => g.Key switch { "" => 0, "keyboard" => 1, _ => 2 })
                .Select(group =>
                {
                    var items = group.Select(s => _items[s.Key]).ToList();
                    return group.Key switch
                    {
                        "keyboard" => new GameSettingsGroup("Keyboard",
                            "The keys you play with in the game window. Binding a key another input has swaps the two.", items),
                        "controller" => new GameSettingsGroup("Controller",
                            "A controller's buttons. Controller default keeps the usual layout for its type.", items),
                        _ => new GameSettingsGroup(group.First().TabLabel, "", items),
                    };
                }).ToList();
            Sections.Add(new GameSettingsSection(tab.First().TabLabel, groups));
        }
        SelectedSection = Sections.FirstOrDefault(s => s.Title == selected) ?? Sections.FirstOrDefault();
    }

    private void OnRunningChanged(bool running) => ReloadAsync();

    public void Dispose()
    {
        _settings.Game.RunningChanged -= OnRunningChanged;
    }
}
