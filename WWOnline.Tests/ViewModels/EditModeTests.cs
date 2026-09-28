using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

/// <summary>
/// The Room / Room items pages are read-only until the room owner presses Edit. CanEdit is
/// driven by the pages from "is room owner (and connected)"; these cover the lock itself.
/// </summary>
public class EditModeTests
{
    [Fact]
    public void StartsLocked()
    {
        var edit = new EditMode();
        Assert.False(edit.IsEditing);
        Assert.True(edit.IsViewing);
        Assert.False(edit.CanEdit);
    }

    [Fact]
    public void Owner_CanEnterEditMode_AndDoneLocksAgain()
    {
        var edit = new EditMode { CanEdit = true };

        Assert.True(edit.Begin());
        Assert.True(edit.IsEditing);
        Assert.False(edit.IsViewing);

        edit.End();
        Assert.False(edit.IsEditing);
        Assert.True(edit.IsViewing);
    }

    [Fact]
    public void NonOwner_CannotEnterEditMode()
    {
        var edit = new EditMode { CanEdit = false };

        Assert.False(edit.Begin());
        Assert.False(edit.IsEditing);
    }

    [Fact]
    public void LosingOwnership_WhileEditing_LocksImmediately()
    {
        var edit = new EditMode { CanEdit = true };
        edit.Begin();

        edit.CanEdit = false; // ownership moved to another player, or the connection dropped

        Assert.False(edit.IsEditing);
        Assert.False(edit.Begin());
    }

    [Fact]
    public void RegainingOwnership_DoesNotReopenEditMode()
    {
        var edit = new EditMode { CanEdit = true };
        edit.Begin();
        edit.CanEdit = false;

        edit.CanEdit = true;

        Assert.False(edit.IsEditing); // must press Edit again
    }

    [Fact]
    public void RaisesPropertyChanged_ForIsEditingAndIsViewing()
    {
        var edit = new EditMode { CanEdit = true };
        var changed = new List<string?>();
        edit.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        edit.Begin();

        Assert.Contains(nameof(EditMode.IsEditing), changed);
        Assert.Contains(nameof(EditMode.IsViewing), changed);
    }

    [Theory]
    [InlineData("Player1", "P1")]
    [InlineData("Jake Smith", "JS")]
    [InlineData("Link", "Li")]
    [InlineData("", "?")]
    public void AvatarInitials(string name, string expected) =>
        Assert.Equal(expected, TunicColors.InitialsOf(name));
}
