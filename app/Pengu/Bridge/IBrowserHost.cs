namespace Pengu.Bridge;

/// <summary>
/// Platform-neutral transport interface that the per-window <see cref="JsBridge"/>
/// needs from a webview implementation. Heads (Pengu.Windows / Pengu.MacOS)
/// provide concrete implementations wrapping WebView2 / WKWebView.
/// </summary>
public interface IBrowserHost
{
    /// <summary>Fired when the renderer posts a message via
    /// <c>chrome.webview.postMessage(obj)</c>. The argument is the message
    /// serialized as JSON (the WebView2 "as JSON" path).</summary>
    event Action<string>? WebMessageReceivedAsJson;

    /// <summary>Send a JSON-encoded object to the renderer. The renderer
    /// receives it on <c>chrome.webview.message</c>; the bridge shim parses
    /// it as <c>{id, ok, result|error}</c> for replies or
    /// <c>{event, ...}</c> for pushes.</summary>
    void PostWebMessageAsJson(string json);

    /// <summary>Inject a JS script that runs at the start of every navigation,
    /// before page scripts. The bridge shim is injected this way so
    /// <c>window.pengu</c> is in place before any user JS runs.</summary>
    void AddScriptToExecuteOnDocumentCreated(string script);
}
