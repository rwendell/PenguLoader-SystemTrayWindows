using AppKit;
using CoreGraphics;
using Foundation;
using Pengu.Bridge;
using Pengu.Logging;
using Pengu.Pack;
using WebKit;

namespace Pengu.MacOS.Browser;

/// <summary>
/// One <see cref="WKWebView"/> hosted as a window's contentView. Counterpart
/// of <c>Pengu.Windows.Browser.Browser</c>; implements <see cref="IBrowserHost"/>
/// so the cross-platform <see cref="JsBridge"/> can drive it identically on
/// both platforms.
///
/// <para>The webview's <c>WKWebViewConfiguration</c> registers a
/// <see cref="HubAssetSchemeHandler"/> for <c>app://</c> when an
/// <see cref="AppDat"/> is supplied (Release / packed builds). Dev mode
/// (<see cref="AppEnv.DevUrl"/> set) skips the handler and navigates straight
/// to the Vite dev server URL.</para>
/// </summary>
internal sealed class WkWebViewHost : NSObject, IBrowserHost, IWKScriptMessageHandler
{
    /// <summary>JS-side handler name. Renderer posts via
    /// <c>window.webkit.messageHandlers.pengu.postMessage(jsonString)</c>.</summary>
    private const string MessageHandlerName = "pengu";

    private readonly WKWebView _webView;
    private readonly NavigationLogger _navDelegate;

    public event Action<string>? WebMessageReceivedAsJson;

    /// <summary>The view to mount as the window's contentView.</summary>
    public NSView View => _webView;

    public WkWebViewHost(AppDat? packedAppDat)
    {
        var config = new WKWebViewConfiguration();

        // Custom URL scheme handler for the packed bundle. Always registered
        // even when packedAppDat is null — handler returns 404 for every
        // request in that case, but the registration is what stops WKWebView
        // from escalating an unknown scheme to NSWorkspace's "what app
        // handles app://?" system prompt.
        var handler = new HubAssetSchemeHandler(packedAppDat);
        config.SetUrlSchemeHandler(handler, "app");

        // User content controller carries (a) the JS shim injected at
        // document-start by Phase D, and (b) our message handler that
        // catches renderer→host posts.
        var ucc = new WKUserContentController();
        ucc.AddScriptMessageHandler(this, MessageHandlerName);
        config.UserContentController = ucc;

        // DevTools (Web Inspector) on in --dev mode, off otherwise. WKWebView
        // exposes this via Configuration.Preferences.SetValueForKey since
        // the public Inspectable property is macOS 13.3+.
        if (AppEnv.DevUrl is not null)
        {
            using var trueObj = NSNumber.FromBoolean(true);
            config.Preferences.SetValueForKey(trueObj, (NSString)"developerExtrasEnabled");
        }

        _webView = new WKWebView(CGRect.Empty, config);
        _webView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;

        _navDelegate = new NavigationLogger();
        _webView.NavigationDelegate = _navDelegate;
    }

    public void Navigate(string url)
    {
        var nsUrl = NSUrl.FromString(url)
            ?? throw new ArgumentException($"Invalid URL: {url}", nameof(url));
        _webView.LoadRequest(new NSUrlRequest(nsUrl));
        Log.Info("WKWebView navigating to {0}", url);
    }

    public void PostWebMessageAsJson(string json)
    {
        // Bridge protocol: dispatch a CustomEvent on window so the JS shim's
        // listener picks up replies and pushes uniformly. Mirror of the
        // chrome.webview.message path on Windows.
        var script = $"window.dispatchEvent(new CustomEvent('pengu:message',{{detail:{json}}}));";
        _webView.EvaluateJavaScript(script, (result, error) =>
        {
            if (error is not null)
                Log.Warn("PostWebMessageAsJson failed: {0}", error.LocalizedDescription);
        });
    }

    public void AddScriptToExecuteOnDocumentCreated(string script)
    {
        var userScript = new WKUserScript(
            (NSString)script,
            WKUserScriptInjectionTime.AtDocumentStart,
            isForMainFrameOnly: false);
        _webView.Configuration.UserContentController.AddUserScript(userScript);
    }

    [Export("userContentController:didReceiveScriptMessage:")]
    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        if (message.Name != MessageHandlerName) return;
        // JS side posts JSON.stringify({...}); message.Body comes back as a
        // managed string (NSString -> .NET string) thanks to AppKit bindings.
        var json = message.Body?.ToString();
        if (!string.IsNullOrEmpty(json))
            WebMessageReceivedAsJson?.Invoke(json);
    }

    private sealed class NavigationLogger : WKNavigationDelegate
    {
        public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            Log.Info("WKWebView navigation finished: {0}", webView.Url);
        }

        public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        {
            Log.Error("WKWebView navigation failed: {0} for {1}", error.LocalizedDescription, webView.Url);
        }

        public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        {
            Log.Error("WKWebView provisional navigation failed: {0} for {1}", error.LocalizedDescription, webView.Url);
        }
    }
}
