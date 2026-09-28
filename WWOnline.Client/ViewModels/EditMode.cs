using CommunityToolkit.Mvvm.ComponentModel;

namespace WWOnline.ViewModels;

/// <summary>
/// Read-only-by-default edit lock for the Room and Room items pages. The room owner sees
/// everything locked until they press Edit; Done locks it again. Losing the right to edit
/// (ownership moved, disconnected) drops out of edit mode, so nothing stays unlocked by accident.
/// </summary>
public partial class EditMode : ObservableObject
{
    private bool _isEditing;

    /// <summary>Whether Edit is offered at all (the room owner, while connected).</summary>
    [ObservableProperty]
    private bool _canEdit;

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
                OnPropertyChanged(nameof(IsViewing));
        }
    }

    public bool IsViewing => !IsEditing;

    partial void OnCanEditChanged(bool value)
    {
        if (!value) IsEditing = false;
    }

    /// <summary>Enter edit mode. Returns false (and stays locked) when editing isn't allowed.</summary>
    public bool Begin()
    {
        if (!CanEdit) return false;
        IsEditing = true;
        return true;
    }

    /// <summary>Done: lock again.</summary>
    public void End() => IsEditing = false;
}
