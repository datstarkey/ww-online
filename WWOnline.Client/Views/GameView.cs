using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using WWOnline.Native;

namespace WWOnline.Views;

/// <summary>
/// The embedded game in the Avalonia layout (WW_ONLINE_EMBED.md 4.4). macOS: libtww's NSView (CAMetalLayer-backed),
/// which Avalonia adds as a subview and frames; the view itself reports its size and backing scale to the game
/// (tww_set_size) and takes the keyboard when clicked. Nothing Avalonia draws can sit on top of it. One per
/// process: MainWindow moves it between its game page and the pop-out window.
/// </summary>
public sealed partial class GameView : NativeControlHost
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    private readonly GameHost _host;
    private IntPtr _view;

    public GameView(GameHost host) => _host = host;

    /// <summary>Give the game the keyboard, as a click on it does (macOS: its view becomes its window's first
    /// responder). Showing a window gives the keyboard to Avalonia's own view.</summary>
    public void TakeKeyboard()
    {
        if (!OperatingSystem.IsMacOS() || _view == IntPtr.Zero) return;
        var window = objc_msgSend(_view, sel_registerName("window"));
        if (window != IntPtr.Zero)
            objc_msgSend_void(window, sel_registerName("makeFirstResponder:"), _view);
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (OperatingSystem.IsMacOS())
            return new PlatformHandle(_view = _host.CreateView(), "NSView");
        // TODO: tww_view_create(parent.Handle) returns a child HWND (design 4.4) once libtww builds on Windows.
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The native game has no Windows build yet.");
        return base.CreateNativeControlCore(parent);
    }

    // The game cannot restart: its view lives as long as the process (Avalonia only detaches it here).
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
    }

    [LibraryImport(ObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjC)]
    private static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void objc_msgSend_void(IntPtr receiver, IntPtr selector, IntPtr argument);
}
