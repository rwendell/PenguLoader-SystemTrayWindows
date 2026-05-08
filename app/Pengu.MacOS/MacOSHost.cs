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
        // Phase F: register Pengu.MacOS.Activation.RespawnAction (Universal).
        // Phase G:  register Pengu.MacOS.Activation.InsertDylibAction (OnDemand).
        // Empty for now — bridge calls to pengu.activation.* report
        // "not available" until those are wired.
        _ = registry; _ = config; _ = bus;
    }

    public bool IsAdmin() => geteuid() == 0;

    public void MinimizeMainWindow() => _mainWindow?.Miniaturize(_mainWindow);

    public void CloseMainWindow() => _mainWindow?.PerformClose(_mainWindow);

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
        // CSS app-region: drag handles drag for the hub's own titlebar
        // strips. Programmatic drag isn't typically needed on macOS — the
        // window is movable-by-background already (BorderlessWindow sets
        // MovableByWindowBackground = true).
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
