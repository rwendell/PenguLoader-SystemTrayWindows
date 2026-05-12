using AppKit;
using Foundation;
using Pengu;
using Pengu.Logging;

namespace Pengu.MacOS;

internal sealed class AppDelegate : NSApplicationDelegate
{
    private MacOSHost? _host;

    public override async void DidFinishLaunching(NSNotification notification)
    {
        // async void is the standard pattern for AppKit delegate callbacks
        // dispatching to async work — exceptions are caught explicitly here so
        // they don't crash the process via Task.UnobservedTaskException.
        Log.Info("Pengu.MacOS launched (pid={0})", Environment.ProcessId);

        // Login Items have no CLI-arg path, so we detect a login-time launch
        // via the AppleEvent that fired this DidFinishLaunching and flag
        // silent. The kAEOpenApplication event carries a 'prdt' (keyAEPropData)
        // descriptor whose type code is 'lgit' (keyAELaunchedAsLogInItem)
        // when AppKit launches us from a System Events login item.
        if (WasLaunchedAtLogin())
        {
            Log.Info("Launched as a login item; auto-silent");
            AppEnv.MarkSilent();
        }

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

    /// <summary>
    /// Inspect <c>NSAppleEventManager.CurrentAppleEvent</c> to determine
    /// whether AppKit launched us as a System Events login item. Returns
    /// false on any binding/event mismatch — silent-on-startup is a UX
    /// nicety, not a correctness boundary.
    /// </summary>
    /// <remarks>
    /// The 'oapp' (kAEOpenApplication) event, when fired by a login-item
    /// launch, carries a 'prdt' (keyAEPropData) parameter holding an
    /// <c>'enum'</c>-typed descriptor whose enum value is <c>'lgit'</c>
    /// (keyAELaunchedAsLogInItem). The descriptor's own type is
    /// <c>'enum'</c> — the 'lgit' code is read via EnumCodeValue(), not
    /// from the descriptor type itself.
    /// </remarks>
    private static bool WasLaunchedAtLogin()
    {
        try
        {
            const uint kAEPropData            = 0x70726474u; // 'prdt'
            const uint kAELaunchedAsLogInItem = 0x6c676974u; // 'lgit'

            var ev = NSAppleEventManager.SharedAppleEventManager.CurrentAppleEvent;
            if (ev is null)
            {
                Log.Debug("WasLaunchedAtLogin: CurrentAppleEvent is null");
                return false;
            }

            Log.Debug("WasLaunchedAtLogin: eventClass=0x{0:x8} eventID=0x{1:x8}",
                      (uint)ev.EventClass, (uint)ev.EventID);

            var prop = ev.ParamDescriptorForKeyword(kAEPropData);
            if (prop is null)
            {
                Log.Debug("WasLaunchedAtLogin: no 'prdt' param on event");
                return false;
            }

            uint code = prop.EnumCodeValue();
            Log.Debug("WasLaunchedAtLogin: prdt enumCode=0x{0:x8}", code);
            return code == kAELaunchedAsLogInItem;
        }
        catch (Exception ex)
        {
            Log.Debug("WasLaunchedAtLogin failed: {0}", ex.Message);
            return false;
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
