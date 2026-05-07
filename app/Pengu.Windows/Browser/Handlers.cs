using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Diga.WebView2.Interop;

namespace Pengu.Windows.Browser;

/// <summary>
/// COM handler shims that adapt WebView2's callback-style API to async/await
/// via <see cref="TaskCompletionSource"/>. One handler per callback contract;
/// they're tiny and AOT-clean (Diga's COM gen emits the vtable thunks at
/// compile time via <see cref="GeneratedComClassAttribute"/>).
///
/// <para>Anchored on the <see cref="Browser"/> instance to prevent GC during
/// the in-flight call.</para>
/// </summary>

[GeneratedComClass]
internal partial class CreateEnvironmentCompletedHandler
    : ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler
{
    private readonly TaskCompletionSource<ICoreWebView2Environment> _tcs;
    public CreateEnvironmentCompletedHandler(TaskCompletionSource<ICoreWebView2Environment> tcs) => _tcs = tcs;

    public void Invoke(int errorCode, ICoreWebView2Environment? createdEnvironment)
    {
        if (errorCode < 0 || createdEnvironment is null)
            _tcs.TrySetException(new COMException("WebView2 environment creation failed.", errorCode));
        else
            _tcs.TrySetResult(createdEnvironment);
    }
}

[GeneratedComClass]
internal partial class CreateControllerCompletedHandler
    : ICoreWebView2CreateCoreWebView2ControllerCompletedHandler
{
    private readonly TaskCompletionSource<ICoreWebView2Controller> _tcs;
    public CreateControllerCompletedHandler(TaskCompletionSource<ICoreWebView2Controller> tcs) => _tcs = tcs;

    public void Invoke(int errorCode, ICoreWebView2Controller? createdController)
    {
        if (errorCode < 0 || createdController is null)
        {
            // 0x800700AA = ERROR_BUSY: a stale msedgewebview2.exe from a prior
            // run still holds the user-data folder lock. Mitigation guidance is
            // in the message; users can also delete %LOCALAPPDATA%\.pengu\WebView2.
            string detail = (uint)errorCode == 0x800700AA
                ? "WebView2 controller creation failed (ERROR_BUSY 0x800700AA): user-data folder locked by a stale msedgewebview2.exe. Kill leftover msedgewebview2.exe processes or delete %LOCALAPPDATA%\\.pengu\\WebView2."
                : "WebView2 controller creation failed.";
            _tcs.TrySetException(new COMException(detail, errorCode));
        }
        else
            _tcs.TrySetResult(createdController);
    }
}

[GeneratedComClass]
internal partial class WebMessageReceivedHandler
    : ICoreWebView2WebMessageReceivedEventHandler
{
    private readonly Action<ICoreWebView2WebMessageReceivedEventArgs> _onReceived;
    public WebMessageReceivedHandler(Action<ICoreWebView2WebMessageReceivedEventArgs> onReceived) => _onReceived = onReceived;

    public void Invoke(ICoreWebView2 sender, ICoreWebView2WebMessageReceivedEventArgs args)
    {
        try { _onReceived(args); } catch (Exception ex) { Pengu.Logging.Log.Error(ex, "WebMessageReceived handler threw"); }
    }
}

[GeneratedComClass]
internal partial class AddScriptCompletedHandler
    : ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler
{
    public void Invoke(int errorCode, string id) { /* no-op */ }
}

[GeneratedComClass]
internal partial class NavigationCompletedHandler
    : ICoreWebView2NavigationCompletedEventHandler
{
    private readonly Action<ICoreWebView2NavigationCompletedEventArgs> _onCompleted;
    public NavigationCompletedHandler(Action<ICoreWebView2NavigationCompletedEventArgs> onCompleted) => _onCompleted = onCompleted;

    public void Invoke(ICoreWebView2 sender, ICoreWebView2NavigationCompletedEventArgs args)
    {
        try { _onCompleted(args); } catch (Exception ex) { Pengu.Logging.Log.Error(ex, "NavigationCompleted handler threw"); }
    }
}
