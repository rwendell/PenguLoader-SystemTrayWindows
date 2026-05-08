using AppKit;
using Foundation;
using Pengu.Logging;

namespace Pengu.MacOS;

internal sealed class AppDelegate : NSApplicationDelegate
{
    public override void DidFinishLaunching(NSNotification notification)
    {
        Log.Info("Pengu.MacOS launched (pid={0})", Environment.ProcessId);

        // Phase A scaffolding: app boots, sits in the run loop, and exits via
        // Cmd-Q or NSApplication.terminate. Phase B opens the BorderlessWindow;
        // Phase D wires up AppHost.RunAsync against MacOSHost (the IHost impl).
    }

    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
    {
        // Daemon must outlive the hub window: LcuxWatcher (Universal mode) and
        // RcsDaemon (OnDemand mode) both need the process resident to fire
        // activation when LCUX launches. Tray (Phase H) is how the user
        // interacts with the daemon after closing the window.
        return false;
    }

    public override void WillTerminate(NSNotification notification)
    {
        Log.Info("Pengu.MacOS terminating");
    }
}
