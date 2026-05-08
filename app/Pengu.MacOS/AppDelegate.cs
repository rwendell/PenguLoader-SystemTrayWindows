using AppKit;
using Foundation;
using Pengu;
using Pengu.Logging;
using Pengu.MacOS.Browser;
using Pengu.MacOS.Window;
using Pengu.Pack;

namespace Pengu.MacOS;

internal sealed class AppDelegate : NSApplicationDelegate
{
    private BorderlessWindow? _mainWindow;
    private WkWebViewHost?    _browser;
    private AppDat?           _appDat;

    public override void DidFinishLaunching(NSNotification notification)
    {
        Log.Info("Pengu.MacOS launched (pid={0})", Environment.ProcessId);

        // Phase B: window. Phase C: WKWebView mounted as its contentView.
        // Phase D will refactor to go through MacOSHost / AppHost.RunAsync so
        // bridge handlers attach before the first navigation.
        _mainWindow = new BorderlessWindow();

        // Open app.dat if it's next to the binary (Release builds). In Debug
        // we typically rely on --dev=URL and don't need the bundle.
        var datPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.dat");
        if (System.IO.File.Exists(datPath))
        {
            try
            {
                _appDat = AppDat.Open(datPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open app.dat at {0}", datPath);
            }
        }

        _browser = new WkWebViewHost(_appDat);
        _mainWindow.ContentView = _browser.View;

        // Resolve URL: --dev wins, else the packed app://hub/ scheme. Same
        // resolution as Pengu/AppHost.cs uses for Windows.
        var url = AppEnv.DevUrl ?? "app://hub/";
        _browser.Navigate(url);

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
