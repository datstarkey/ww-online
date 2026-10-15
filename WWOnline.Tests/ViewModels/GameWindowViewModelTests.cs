using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public sealed class GameWindowViewModelTests
{
    private int _docked;
    private readonly List<string> _changes = [];

    private GameWindowViewModel Vm()
    {
        var vm = new GameWindowViewModel(() => _docked++);
        vm.PropertyChanged += (_, e) => _changes.Add(e.PropertyName!);
        return vm;
    }

    [Fact]
    public void StartsDocked_Windowed()
    {
        var vm = Vm();
        Assert.False(vm.IsPoppedOut);
        Assert.False(vm.IsFullScreen);
    }

    [Fact]
    public void PopOut_ThenDock_ShowsTheGamePageAgain()
    {
        var vm = Vm();
        vm.PopOutCommand.Execute(null);
        Assert.True(vm.IsPoppedOut);
        Assert.False(vm.IsFullScreen);
        Assert.Equal(0, _docked);

        vm.DockCommand.Execute(null);
        Assert.False(vm.IsPoppedOut);
        Assert.Equal(1, _docked);
    }

    [Fact]
    public void Dock_WhileDocked_DoesNothing()
    {
        var vm = Vm();
        vm.DockCommand.Execute(null);
        Assert.Equal(0, _docked);
        Assert.Empty(_changes);
    }

    [Fact]
    public void FullScreen_WhileDocked_PopsOutFullScreen_FullScreenFirst()
    {
        var vm = Vm();
        vm.ToggleFullScreenCommand.Execute(null);
        Assert.True(vm.IsPoppedOut);
        Assert.True(vm.IsFullScreen);
        // The pop-out window is created full screen rather than going full screen after it shows.
        Assert.Equal([nameof(vm.IsFullScreen), nameof(vm.IsPoppedOut)], _changes);
    }

    [Fact]
    public void FullScreen_WhilePoppedOut_Toggles_AndStaysPoppedOut()
    {
        var vm = Vm();
        vm.PopOutCommand.Execute(null);
        vm.ToggleFullScreenCommand.Execute(null);
        Assert.True(vm.IsFullScreen);
        vm.ToggleFullScreenCommand.Execute(null);
        Assert.False(vm.IsFullScreen);
        Assert.True(vm.IsPoppedOut);
    }

    [Fact]
    public void Dock_FromFullScreen_ClosesTheWindowFirst_ThenLeavesFullScreen()
    {
        var vm = Vm();
        vm.ToggleFullScreenCommand.Execute(null);
        _changes.Clear();

        vm.DockCommand.Execute(null);
        Assert.False(vm.IsPoppedOut);
        Assert.False(vm.IsFullScreen);
        Assert.Equal(1, _docked);
        // The window goes before full screen ends, so it never animates out of full screen on its way out.
        Assert.Equal([nameof(vm.IsPoppedOut), nameof(vm.IsFullScreen)], _changes);
    }

    [Fact]
    public void FullScreen_SetByTheWindow_IsKept_AndPopOutAgainStartsWindowed()
    {
        var vm = Vm();
        vm.PopOutCommand.Execute(null);
        vm.IsFullScreen = true; // the green button
        vm.DockCommand.Execute(null);
        vm.PopOutCommand.Execute(null);
        Assert.True(vm.IsPoppedOut);
        Assert.False(vm.IsFullScreen);
    }
}
