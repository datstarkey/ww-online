using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Services;

namespace WWOnline.ViewModels;

public partial class GameLogViewModel : ViewModelBase, IDisposable
{
    private readonly GameLogService _gameLog;
    private bool _disposed;

    public ObservableCollection<GameLogEntry> Entries => _gameLog.Entries;

    /// <summary>Newest entries, for the Room page's "What's happening" feed.</summary>
    public ObservableCollection<GameLogEntry> RecentEntries { get; } = new();

    private const int RecentCount = 6;

    [ObservableProperty]
    private bool _hasEntries;

    public GameLogViewModel(GameLogService gameLog)
    {
        _gameLog = gameLog;
        _gameLog.Entries.CollectionChanged += OnEntriesChanged;
        _hasEntries = _gameLog.Entries.Count > 0;
        RebuildRecent();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasEntries = _gameLog.Entries.Count > 0;
        RebuildRecent();
    }

    private void RebuildRecent()
    {
        // Entries are newest-first (inserted at index 0); take the top N.
        RecentEntries.Clear();
        for (int i = 0; i < _gameLog.Entries.Count && i < RecentCount; i++)
            RecentEntries.Add(_gameLog.Entries[i]);
    }

    [RelayCommand]
    private void Clear() => _gameLog.Clear();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameLog.Entries.CollectionChanged -= OnEntriesChanged;
    }
}
