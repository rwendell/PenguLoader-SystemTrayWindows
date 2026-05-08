using AppKit;
using CoreGraphics;
using Foundation;
using Pengu.Logging;

namespace Pengu.MacOS.Window;

/// <summary>
/// Frameless NSWindow that the SolidJS hub paints inside. No native titlebar
/// — the hub renders its own. Close button hides the window instead of
/// closing it (the daemon must outlive the window so LcuxWatcher / RcsDaemon
/// stay armed for the next LoL launch). Cmd-Q still terminates normally.
/// </summary>
internal sealed class BorderlessWindow : NSWindow
{
    private const float DefaultWidth  = 1000f;
    private const float DefaultHeight = 700f;

    public BorderlessWindow()
        : base(
            new CGRect(0, 0, DefaultWidth, DefaultHeight),
            // No .Titled — hub renders its own titlebar.
            NSWindowStyle.Resizable
                | NSWindowStyle.Closable
                | NSWindowStyle.Miniaturizable
                | NSWindowStyle.FullSizeContentView,
            NSBackingStore.Buffered,
            deferCreation: false)
    {
        // Hub renders its own titlebar; hide the native one but keep the
        // traffic-light buttons (close/min/zoom) visible at their default
        // location — the hub layout reserves space for them.
        TitlebarAppearsTransparent = true;
        TitleVisibility = NSWindowTitleVisibility.Hidden;
        Title = "Pengu";

        // Transparent compositor so the hub's CSS background paints through;
        // also lets NSVisualEffectView (Phase D bridge: window.Effect.apply)
        // be installed beneath the WKWebView for vibrancy/blur.
        BackgroundColor = NSColor.Clear;
        IsOpaque = false;
        HasShadow = true;

        // Allow click-anywhere-and-drag for the body of the frameless window
        // when no element claims the click. Plays nice with `app-region: drag`
        // CSS in the hub for the explicit dragbar regions.
        MovableByWindowBackground = true;

        // Keep the NSObject alive across hide/show. Default would dealloc on
        // close (orderOut) and re-show would crash.
        ReleaseWhenClosed(false);

        Center();

        Delegate = new BorderlessWindowDelegate();
    }

    // NSWindow defaults canBecomeKeyWindow/Main to NO when the styleMask lacks
    // .Titled — which means MakeKeyAndOrderFront only orders front, never
    // makes us key. Without key status the WKWebView gets no mouse tracking
    // (hover doesn't fire) and the menu bar shows no active app. We dropped
    // .Titled deliberately so the hub paints its own titlebar; opt back in
    // here so the window is fully interactive.
    public override bool CanBecomeKeyWindow  => true;
    public override bool CanBecomeMainWindow => true;

    public void ShowAndFocus()
    {
        MakeKeyAndOrderFront(null);
        NSApplication.SharedApplication.Activate();
    }

    private sealed class BorderlessWindowDelegate : NSWindowDelegate
    {
        public override bool WindowShouldClose(NSObject sender)
        {
            // Red traffic-light click → hide instead of closing. The daemon
            // continues running (the macOS Tray in Phase H is how the user
            // re-opens the window). Returning false suppresses the close.
            if (sender is NSWindow w)
            {
                Log.Info("Main window close requested; hiding to background");
                w.OrderOut(sender);
            }
            return false;
        }
    }
}
