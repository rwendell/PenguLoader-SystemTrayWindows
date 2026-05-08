using AppKit;
using Foundation;
using Pengu.Logging;
using Pengu.MacOS.Window;

namespace Pengu.MacOS;

internal sealed class AppDelegate : NSApplicationDelegate
{
    private BorderlessWindow? _mainWindow;

    public override void DidFinishLaunching(NSNotification notification)
    {
        Log.Info("Pengu.MacOS launched (pid={0})", Environment.ProcessId);

        // Phase B: open the BorderlessWindow placeholder. Phase C swaps the
        // window's contentView for a WKWebView pointed at app://hub/ (or the
        // Vite dev server when --dev=URL is set). Phase D wires AppHost.RunAsync
        // through MacOSHost so the bridge handlers attach before navigation.
        _mainWindow = new BorderlessWindow();
        _mainWindow.ShowAndFocus();
    }

    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
    {
        // Daemon must outlive the hub window: LcuxWatcher (Universal mode) and
        // RcsDaemon (OnDemand mode) both need the process resident to fire
        // activation when LCUX launches. Tray (Phase H) is how the user
        // interacts with the daemon after closing the window.
        return false;
    }

    public override bool ApplicationShouldHandleReopen(NSApplication sender, bool hasVisibleWindows)
    {
        // Triggered when (a) a second launch's NSRunningApplication.Activate
        // hits us via SingleInstance, or (b) the user clicks our Dock icon
        // while no window is showing. Either way: bring the main window back.
        if (!hasVisibleWindows && _mainWindow is not null)
        {
            Log.Info("Reopen requested; un-hiding main window");
            _mainWindow.ShowAndFocus();
        }
        return true;
    }

    public override void WillTerminate(NSNotification notification)
    {
        Log.Info("Pengu.MacOS terminating");
    }
}
