using Pengu.Bridge;

namespace Pengu;

/// <summary>
/// Platform abstraction surface implemented by each head (Pengu.Windows /
/// Pengu.MacOS). <see cref="AppHost"/> orchestrates startup against this
/// interface so the same control flow runs on both platforms.
///
/// <para>Methods marked async run on the dispatcher thread; the implementation
/// MUST not block (use the head's dispatcher to schedule work).</para>
/// </summary>
public interface IHost
{
    /// <summary>Path to the user data root, e.g. <c>%LOCALAPPDATA%\.pengu\</c>
    /// (Windows) or <c>~/Library/Application Support/Pengu/</c> (macOS).</summary>
    string DataRoot { get; }

    /// <summary>Path to the directory containing the running executable. Used
    /// for finding <c>core.dll</c> / <c>core.dylib</c> and <c>app.dat</c>.</summary>
    string ExeDirectory { get; }

    /// <summary>Whether WebView2 (Win) / WKWebView (Mac) is available. If false,
    /// <see cref="ShowMissingRuntimeDialogAsync"/> must be callable to surface
    /// a user-facing message.</summary>
    bool IsWebViewRuntimeAvailable();

    /// <summary>Surface a TaskDialog (Win) / NSAlert (Mac) explaining the
    /// missing runtime. Caller exits the process after this returns.</summary>
    Task ShowMissingRuntimeDialogAsync();

    /// <summary>Initialize the WebView2 / WKWebView environment. Idempotent;
    /// must be called before opening any window.</summary>
    Task InitializeBrowserEnvironmentAsync();

    /// <summary>Open the main hub window pointed at <paramref name="url"/>.
    /// The window's <see cref="IBrowserHost"/> is used to wire up a
    /// <see cref="JsBridge"/>; bridge handlers in <paramref name="bridgeHandlers"/>
    /// are registered before the JS shim is injected.</summary>
    Task OpenMainWindowAsync(string url, IReadOnlyList<IJsInteropDispatcher> bridgeHandlers);
}
