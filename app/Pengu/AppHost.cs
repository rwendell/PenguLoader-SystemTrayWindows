using Pengu.Bridge;
using Pengu.Logging;

namespace Pengu;

/// <summary>
/// Platform-neutral startup orchestration. Heads call <see cref="RunAsync"/>
/// from their <c>Program.Main</c> after wiring an <see cref="IHost"/>.
///
/// <para>Responsibilities: WebView2/WKWebView runtime check, env init,
/// resolve the main window URL (dev server vs <c>app://hub/</c>), open the
/// window with the bridge handlers attached.</para>
/// </summary>
public static class AppHost
{
    public static async Task<int> RunAsync(IHost host)
    {
        Log.Info("{0} v{1} starting (pid={2}, dataRoot={3})",
            AppEnv.AppName, AppEnv.AppVersion, Environment.ProcessId, host.DataRoot);

        if (!host.IsWebViewRuntimeAvailable())
        {
            Log.Error("WebView runtime not available; surfacing dialog and exiting");
            await host.ShowMissingRuntimeDialogAsync().ConfigureAwait(true);
            return 2;
        }

        try
        {
            await host.InitializeBrowserEnvironmentAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WebView environment initialization failed");
            return 3;
        }

        // Resolve the URL we'll point the main window at.
        // Debug iteration: --dev=<vite-url> bypasses app.dat and goes straight to Vite.
        // Release: navigate to app://hub/ which the scheme handler resolves against app.dat.
        var url = AppEnv.DevUrl ?? "app://hub/";
        Log.Info("Main window navigating to {0}", url);

        // First-cut bridge surface: just the ping smoke-test. Real APIs land in
        // subsequent commits as activation / config / plugins / host / i18n.
        var handlers = new List<IJsInteropDispatcher>
        {
            new Api.PingApi(),
        };

        try
        {
            await host.OpenMainWindowAsync(url, handlers).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open main window");
            return 4;
        }

        Log.Info("Main window opened; entering message loop");
        return 0;
    }
}
