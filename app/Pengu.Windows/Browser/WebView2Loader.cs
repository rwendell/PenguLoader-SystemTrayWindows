using System.Runtime.InteropServices;
using Diga.WebView2.Interop;

namespace Pengu.Windows.Browser;

/// <summary>
/// Raw <c>WebView2Loader.dll</c> P/Invokes. The official
/// <c>Microsoft.Web.WebView2</c> NuGet ships the DLL but its public surface
/// assumes WPF/WinForms; we go through the Diga.WebView2.Interop.AOT COM types
/// for the actual API and use this class only for the global env-creation
/// entrypoint and the runtime-presence probe.
/// </summary>
internal static partial class WebView2Loader
{
    [LibraryImport("WebView2Loader.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int CreateCoreWebView2EnvironmentWithOptions(
        string? browserExeFolder,
        string? userDataFolder,
        [MarshalAs(UnmanagedType.Interface)] ICoreWebView2EnvironmentOptions? environmentOptions,
        [MarshalAs(UnmanagedType.Interface)] ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler handler);

    [LibraryImport("WebView2Loader.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GetAvailableCoreWebView2BrowserVersionString(
        string? browserExeFolder,
        out IntPtr versionInfo);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(IntPtr ptr);

    public static bool IsRuntimeAvailable()
    {
        var hr = GetAvailableCoreWebView2BrowserVersionString(null, out var versionPtr);
        if (hr < 0) return false;
        if (versionPtr != IntPtr.Zero) CoTaskMemFree(versionPtr);
        return true;
    }
}
