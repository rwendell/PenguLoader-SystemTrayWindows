using AppKit;
using Foundation;
using Pengu;
using Pengu.Activation;
using Pengu.Bridge;
using Pengu.Config;
using Pengu.Logging;
using Pengu.MacOS.Browser;
using Pengu.MacOS.Window;
using Pengu.Pack;

namespace Pengu.MacOS;

/// <summary>
/// macOS implementation of <see cref="IHost"/>. Counterpart of
/// <c>Pengu.Windows.WindowsHost</c>.
///
/// <para>WebView runtime: WKWebView ships with macOS, no install check or
/// missing-runtime dialog needed. Browser environment is per-instance, so
/// <see cref="InitializeBrowserEnvironmentAsync"/> is a no-op (vs Windows
/// where WebView2Environment is process-wide and async).</para>
/// </summary>
public sealed partial class MacOSHost : IHost
{
    private BorderlessWindow? _mainWindow;
    private WkWebViewHost?    _browser;
    private AppDat?           _appDat;

    public MacOSHost()
    {
        ExeDirectory = AppContext.BaseDirectory;

        // ~/Library/Application Support/Pengu/ — per-user data root. Distinct
        // from Windows's machine-wide %PROGRAMDATA%\.pengu\ because macOS
        // activation (Universal mode = kill-and-respawn LCUX) is per-user;
        // see docs/app-hub.md §11.
        var appSupport = NSSearchPath.GetDirectories(
            NSSearchPathDirectory.ApplicationSupportDirectory,
            NSSearchPathDomain.User,
            expandTilde: true).FirstOrDefault()
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Library", "Application Support");
        DataRoot = Path.Combine(appSupport, "Pengu");
        Directory.CreateDirectory(DataRoot);
    }

    public string DataRoot { get; }
    public string ExeDirectory { get; }

    public bool IsWebViewRuntimeAvailable() => true;

    public Task ShowMissingRuntimeDialogAsync() => Task.CompletedTask; // never reached

    public Task InitializeBrowserEnvironmentAsync()
    {
        // No process-wide environment: WKWebView is configured per-instance.
        return Task.CompletedTask;
    }

    public Task OpenMainWindowAsync(
        string url,
        IReadOnlyList<IJsInteropDispatcher> bridgeHandlers,
        EventBus bus)
    {
        // Lazy-open app.dat (only used in packed builds; dev mode goes
        // straight to the Vite server URL).
        if (AppEnv.DevUrl is null)
        {
            var datPath = Path.Combine(ExeDirectory, "app.dat");
            if (File.Exists(datPath))
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
        }

        _mainWindow = new BorderlessWindow();
        _browser = new WkWebViewHost(_appDat);
        _mainWindow.ContentView = _browser.View;

        // Wire up the bridge before navigation: shim is injected at
        // document-start via WKUserScript, so it's in place before any
        // page script runs on the new navigation.
        var bridge = new JsBridge(_browser, bus);
        foreach (var h in bridgeHandlers)
            bridge.Register(h);
        bridge.InjectScript();

        _browser.Navigate(url);
        _mainWindow.ShowAndFocus();
        return Task.CompletedTask;
    }

    public void RegisterActivationActions(ActivationActionRegistry registry, ConfigStore config, EventBus bus)
    {
        // Universal mode: kill-and-respawn LCUX with DYLD_INSERT_LIBRARIES.
        // OnDemand fallback (legacy libEGL patch) is intentionally not
        // registered — Universal is the only supported mode on macOS.
        registry.Register(new Pengu.MacOS.Activation.RespawnAction(CoreDylibPath, bus));
        _ = config;
    }

    /// <summary>
    /// Path to <c>core.dylib</c> shipped inside the .app bundle. Resolves to
    /// <c>Pengu.app/Contents/Resources/core.dylib</c> in both dev and release
    /// because <see cref="ExeDirectory"/> is <c>Contents/MonoBundle/</c>
    /// (where .NET stages assemblies in a macOS bundle).
    /// </summary>
    public string CoreDylibPath => Path.GetFullPath(
        Path.Combine(ExeDirectory, "..", "Resources", "core.dylib"));

    public bool IsAdmin() => geteuid() == 0;

    public void MinimizeMainWindow() => _mainWindow?.Miniaturize(_mainWindow);

    public void CloseMainWindow()
    {
        // The hub renders its own close button (no native traffic-light since
        // we drop .Titled). PerformClose on a borderless NSWindow beeps and
        // bails — there's no native button to "perform" against. Skip the
        // close lifecycle entirely and OrderOut: the daemon stays alive,
        // LcuxWatcher keeps watching, the user can re-summon via Dock click.
        _mainWindow?.OrderOut(_mainWindow);
    }

    /// <summary>
    /// Bring the hidden main window back to front. Called from
    /// <c>AppDelegate.ApplicationShouldHandleReopen</c> when a second-launch
    /// activates us, or when the user clicks the Dock icon while no window
    /// is showing.
    /// </summary>
    public void BringMainWindowToFront()
    {
        if (_mainWindow is null) return;
        _mainWindow.MakeKeyAndOrderFront(null);
        NSApplication.SharedApplication.Activate();
    }

    public void StartDragging()
    {
        var window = _mainWindow;
        if (window is null)
        {
            Log.Debug("StartDragging: no main window");
            return;
        }

        // Bridge is async, so NSApplication.CurrentEvent is stale by the time
        // we run; synthesize a fresh NSEvent at the current cursor position.
        // performWindowDrag uses the event for tracking-start coordinates and
        // timestamp — a synthesized event works as long as it's recent.
        var screenLoc = NSEvent.CurrentMouseLocation;
        var winLoc    = window.ConvertPointFromScreen(screenLoc);
        var ev = NSEvent.MouseEvent(
            NSEventType.LeftMouseDown,
            winLoc,
            0,
            NSDate.Now.SecondsSinceReferenceDate,
            window.WindowNumber,
            null,
            0, 1, 0.0f);

        if (ev is null)
        {
            Log.Warn("StartDragging: NSEvent.MouseEvent returned null (winLoc={0})", winLoc);
            return;
        }

        Log.Debug("StartDragging: PerformWindowDrag at winLoc={0} (screen={1})", winLoc, screenLoc);
        window.PerformWindowDrag(ev);
    }

    public Task<string?> PickFolderAsync(string? initialPath)
    {
        var panel = new NSOpenPanel
        {
            CanChooseDirectories = true,
            CanChooseFiles = false,
            AllowsMultipleSelection = false,
            ShowsHiddenFiles = false,
        };
        if (!string.IsNullOrEmpty(initialPath))
        {
            var url = NSUrl.FromFilename(initialPath);
            if (url is not null) panel.DirectoryUrl = url;
        }

        var rc = panel.RunModal();
        if (rc != 1 /* NSModalResponseOK */) return Task.FromResult<string?>(null);
        return Task.FromResult(panel.Urls.FirstOrDefault()?.Path);
    }

    public bool StartupIsEnabled()
    {
        // Phase I: ~/Library/LaunchAgents/com.pengu.lol.plist. Stub for now.
        return false;
    }

    public void SetStartupEnabled(bool enabled)
    {
        // Phase I.
        _ = enabled;
    }

    [System.Runtime.InteropServices.LibraryImport("/usr/lib/libSystem.dylib")]
    private static partial uint geteuid();
}
