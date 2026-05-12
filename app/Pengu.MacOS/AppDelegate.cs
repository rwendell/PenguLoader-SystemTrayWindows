using AppKit;
using Foundation;
using Pengu;
using Pengu.Logging;

namespace Pengu.MacOS;

internal sealed class AppDelegate : NSApplicationDelegate
{
    private MacOSHost? _host;

    public override void WillFinishLaunching(NSNotification notification)
    {
        // Opt out of macOS's "Reopen apps when logging back in" — without
        // this, every reboot launches Pengu twice: once via session
        // restore (no args, window opens) and once via our LaunchAgent
        // (--silent, exits when it sees the restored peer). Net effect:
        // the user sees a window after every reboot even with
        // "Start at login" set to "silent". Call it on every launch
        // because macOS re-enables relaunch any time the user opens the
        // app manually.
        NSApplication.SharedApplication.DisableRelaunchOnLogin();
    }

    public override async void DidFinishLaunching(NSNotification notification)
    {
        // async void is the standard pattern for AppKit delegate callbacks
        // dispatching to async work — exceptions are caught explicitly here so
        // they don't crash the process via Task.UnobservedTaskException.
        //
        // Silent-on-startup arrives as a real CLI flag (--silent) thanks to
        // the LaunchAgent plist's ProgramArguments. AppEnv.ParseCommandLine
        // has already set AppEnv.Silent before this runs (Program.Main parses
        // before NSApplication.Init), so no Apple-event sniffing is needed.
        Log.Info("Pengu.MacOS launched (pid={0})", Environment.ProcessId);

        try
        {
            _host = new MacOSHost();
            int rc = await AppHost.RunAsync(_host).ConfigureAwait(true);
            if (rc != 0)
                Log.Warn("AppHost.RunAsync returned non-zero ({0})", rc);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Pengu startup failed");
            NSApplication.SharedApplication.Terminate(this);
        }
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
        // Fires when (a) a second launch's NSRunningApplication.Activate
        // hits us via SingleInstance, or (b) the user clicks our Dock icon
        // while no window is showing. Bring the (potentially-hidden) main
        // window forward.
        if (!hasVisibleWindows && _host is not null)
        {
            Log.Info("Reopen requested; un-hiding main window");
            _host.BringMainWindowToFront();
        }
        return true;
    }

    public override void WillTerminate(NSNotification notification)
    {
        Log.Info("Pengu.MacOS terminating");
    }
}
