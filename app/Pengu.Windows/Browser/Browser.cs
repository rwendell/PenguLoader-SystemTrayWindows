using Diga.WebView2.Interop;
using Pengu.Bridge;
using Pengu.Logging;
using Vanara.PInvoke;

namespace Pengu.Windows.Browser;

/// <summary>
/// One WebView2 instance hosted inside an HWND. Initialized async via
/// <see cref="InitializeAsync"/>. Owns its controller, webview, and event
/// handlers; releases all of them in <see cref="Close"/>.
///
/// <para>Implements <see cref="IBrowserHost"/> so <see cref="JsBridge"/> can
/// drive it without knowing about WebView2 specifics.</para>
/// </summary>
public sealed class Browser : IBrowserHost
{
    private readonly HWND _hwnd;
    // Anchor handlers so the unmanaged side can call into them without GC.
    private readonly List<object> _handlers = new();

    private ICoreWebView2Controller? _controller;
    private ICoreWebView2? _webView;

    public bool IsReady => _webView is not null;

    public event Action<string>? WebMessageReceivedAsJson;
    public event Action? NavigationCompleted;

    public Browser(HWND hwnd)
    {
        if (hwnd.IsNull) throw new ArgumentException("HWND must be valid.", nameof(hwnd));
        _hwnd = hwnd;
    }

    public async Task InitializeAsync(ICoreWebView2Environment environment)
    {
        if (_controller is not null) throw new InvalidOperationException("Browser already initialized.");
        Log.Debug("Creating WebView2 controller for hwnd={0:x}", _hwnd.DangerousGetHandle());

        var tcs = new TaskCompletionSource<ICoreWebView2Controller>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CreateControllerCompletedHandler(tcs);
        _handlers.Add(handler);

        environment.CreateCoreWebView2Controller(_hwnd.DangerousGetHandle(), handler);

        _controller = await tcs.Task.ConfigureAwait(true);
        _controller.SetIsVisible(1);

        _webView = _controller.GetCoreWebView2();
        ConfigureSettings(_webView);
        WireEvents(_webView);

        Log.Info("Browser ready");
    }

    private void ConfigureSettings(ICoreWebView2 wv)
    {
        var s = wv.GetSettings();
        s.SetIsScriptEnabled(1);
        s.SetIsWebMessageEnabled(1);
        // DevTools + default context menus on in Debug, off in Release. AppEnv
        // doesn't yet expose this knob; Debug == "dev URL is set" is good enough
        // for now, will refine when we add explicit `--dev-tools` CLI later.
        int devtools = AppEnv.DevUrl is not null ? 1 : 0;
        s.SetAreDevToolsEnabled(devtools);
        s.SetAreDefaultContextMenusEnabled(devtools);
        s.SetAreHostObjectsAllowed(0);
    }

    private void WireEvents(ICoreWebView2 wv)
    {
        var msgHandler = new WebMessageReceivedHandler(args =>
        {
            string? json = null;
            try { json = args.GetwebMessageAsJson(); } catch { /* not JSON */ }
            if (!string.IsNullOrEmpty(json))
                WebMessageReceivedAsJson?.Invoke(json);
        });
        _handlers.Add(msgHandler);
        wv.add_WebMessageReceived(msgHandler, out _);

        var navHandler = new NavigationCompletedHandler(_ => NavigationCompleted?.Invoke());
        _handlers.Add(navHandler);
        wv.add_NavigationCompleted(navHandler, out _);
    }

    public void Navigate(string url)
    {
        if (_webView is null) return;
        Log.Debug("Browser navigate: {0}", url);
        _webView.Navigate(url);
    }

    public void PostWebMessageAsJson(string json) => _webView?.PostWebMessageAsJson(json);

    public void AddScriptToExecuteOnDocumentCreated(string script)
    {
        if (_webView is null) return;
        var h = new AddScriptCompletedHandler();
        _handlers.Add(h);
        _webView.AddScriptToExecuteOnDocumentCreated(script, h);
    }

    /// <summary>
    /// Subscribe to <c>WebResourceRequested</c> for URLs matching
    /// <paramref name="uriFilter"/>. The filter follows WebView2's wildcard
    /// syntax (e.g. <c>"app://*"</c>). Used by <see cref="AppSchemeHandler"/>
    /// to serve packed assets for <c>app://hub/</c>.
    /// </summary>
    public void AddWebResourceRequestedFilter(string uriFilter, Action<ICoreWebView2WebResourceRequestedEventArgs> onRequest)
    {
        if (_webView is null) return;
        _webView.AddWebResourceRequestedFilter(uriFilter, COREWEBVIEW2_WEB_RESOURCE_CONTEXT.COREWEBVIEW2_WEB_RESOURCE_CONTEXT_ALL);
        var h = new WebResourceRequestedHandler(onRequest);
        _handlers.Add(h);
        _webView.add_WebResourceRequested(h, out _);
    }

    /// <summary>Resize the WebView2 controller to fill the parent's client area.</summary>
    public void ResizeToFill()
    {
        if (_controller is null) return;
        if (User32.GetClientRect(_hwnd, out var rc))
        {
            _controller.SetBounds(new tagRECT
            {
                left = rc.left, top = rc.top, right = rc.right, bottom = rc.bottom,
            });
        }
    }

    public void Close()
    {
        if (_controller is null) return;
        try { _controller.Close(); }
        catch { /* swallow during teardown */ }
        _controller = null;
        _webView = null;
        _handlers.Clear();
    }
}
