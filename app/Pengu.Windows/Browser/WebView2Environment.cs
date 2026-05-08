using System.Runtime.InteropServices;
using Diga.WebView2.Interop;
using Pengu.Logging;

namespace Pengu.Windows.Browser;

/// <summary>
/// Process-wide WebView2 environment. Created once at startup; every
/// <see cref="Browser"/> in the app uses the same environment so they share
/// cookie storage / IndexedDB / service-worker scope.
///
/// <para>Custom schemes must be registered at env-init time (later
/// registrations are silently ignored). The hub registers <c>app://</c> for
/// the packed asset scheme once we wire up <c>app.dat</c>; for now the env
/// has no custom schemes — Debug builds load from the Vite dev server.</para>
/// </summary>
public sealed class WebView2Environment
{
    private static WebView2Environment? s_instance;

    public static WebView2Environment Instance =>
        s_instance ?? throw new InvalidOperationException("WebView2Environment.InitializeAsync has not been called.");

    public static bool IsInitialized => s_instance is not null;

    internal ICoreWebView2Environment Native { get; }

    private WebView2Environment(ICoreWebView2Environment env)
    {
        Native = env;
    }

    public static async Task InitializeAsync(string userDataFolder, string? additionalArgs = null)
    {
        if (s_instance is not null)
            throw new InvalidOperationException("WebView2Environment already initialized.");
        if (!WebView2Loader.IsRuntimeAvailable())
            throw new InvalidOperationException("WebView2 runtime is not installed on this machine.");

        Directory.CreateDirectory(userDataFolder);
        Log.Info("Initializing WebView2 env: userDataFolder={0}", userDataFolder);

        // msWebView2EnableDraggableRegions: lets the page use `app-region: drag`
        // CSS to mark its HTML titlebar as a Win32 drag region, so the SolidJS
        // header drags the host window without explicit IPC.
        var args = additionalArgs ?? "--enable-features=msWebView2EnableDraggableRegions";

        // Register `app://` as a custom scheme up front. Without this, navigations
        // to `app://hub/...` silently bail and `WebResourceRequested` never fires.
        // treatAsSecure + hasAuthorityComponent so app:// behaves like https for
        // service-worker / secure-context APIs.
        var schemes = new ICoreWebView2CustomSchemeRegistration[]
        {
            new CustomSchemeRegistration("app", treatAsSecure: true, hasAuthorityComponent: true),
        };
        var opts = new WebView2EnvironmentOptions(args, schemes);

        var tcs = new TaskCompletionSource<ICoreWebView2Environment>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CreateEnvironmentCompletedHandler(tcs);

        var hr = WebView2Loader.CreateCoreWebView2EnvironmentWithOptions(null, userDataFolder, opts, handler);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);

        var env = await tcs.Task.ConfigureAwait(true);
        s_instance = new WebView2Environment(env);
        Log.Info("WebView2 environment ready");
    }
}
