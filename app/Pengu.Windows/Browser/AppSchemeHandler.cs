using Diga.WebView2.Interop;
using Pengu.Logging;
using Pengu.Pack;

namespace Pengu.Windows.Browser;

/// <summary>
/// Wires WebView2's <c>WebResourceRequested</c> for <c>app://*</c> URLs to
/// the packed <see cref="AppDat"/>. Must be called per <see cref="Browser"/>
/// after <c>InitializeAsync</c> completes, and only when the host is in
/// packed mode (Release builds; Debug navigates to the Vite dev server and
/// never invokes this).
/// </summary>
internal static class AppSchemeHandler
{
    public static void Attach(Browser browser, AppDat dat, ICoreWebView2Environment env)
    {
        browser.AddWebResourceRequestedFilter("app://*", args => HandleRequest(args, dat, env));
    }

    private static void HandleRequest(
        ICoreWebView2WebResourceRequestedEventArgs args,
        AppDat dat,
        ICoreWebView2Environment env)
    {
        try
        {
            var req = args.GetRequest();
            var url = req.Geturi();

            // app://hub/path/to/file -> host=hub, path=path/to/file. Today
            // there's only one host (the bundle), so we don't switch on it;
            // just resolve the path against the pack. Empty path -> index.html.
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(path)) path = "index.html";

            if (!dat.TryRead(path, out var bytes, out var mime))
            {
                Log.Debug("app:// 404 {0}", url);
                args.SetResponse(env.CreateWebResourceResponse(
                    null!, 404, "Not Found",
                    "Content-Type: text/plain; charset=utf-8"));
                return;
            }

            var stream = new MemoryComStream(bytes);
            var headers = $"Content-Type: {mime}\r\nContent-Length: {bytes.Length}";
            args.SetResponse(env.CreateWebResourceResponse(stream, 200, "OK", headers));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "app:// scheme handler threw");
            try
            {
                args.SetResponse(env.CreateWebResourceResponse(
                    null!, 500, "Internal Error",
                    "Content-Type: text/plain; charset=utf-8"));
            }
            catch { /* swallow during teardown */ }
        }
    }
}
