using System.Runtime.InteropServices;
using Pengu;
using Pengu.Bridge;
using Pengu.Logging;
using Pengu.Windows.Browser;
using Pengu.Windows.Window;

namespace Pengu.Windows;

/// <summary>
/// Windows implementation of <see cref="IHost"/>. Driven by
/// <see cref="Pengu.AppHost.RunAsync"/> from <see cref="Program.Main"/>.
/// </summary>
internal sealed class WindowsHost : IHost
{
    public string DataRoot { get; }
    public string ExeDirectory { get; }

    public WindowsHost()
    {
        ExeDirectory = AppContext.BaseDirectory;
        // %LOCALAPPDATA%\.pengu\ — see docs/app-hub.md §11.
        DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ".pengu");
        Directory.CreateDirectory(DataRoot);
    }

    public bool IsWebViewRuntimeAvailable() => WebView2Loader.IsRuntimeAvailable();

    public Task ShowMissingRuntimeDialogAsync()
    {
        // Real impl will use TaskDialogIndirect with a clickable hyperlink to
        // the MS installer per §5.3 of docs/app-hub.md. Skeleton uses MessageBox
        // as a stand-in until we wire ComCtl32 task dialog plumbing.
        const uint MB_OK = 0x0;
        const uint MB_ICONWARNING = 0x30;
        MessageBoxW(IntPtr.Zero,
            "WebView2 is not installed on your system.\n" +
            "Please install WebView2 from https://developer.microsoft.com/microsoft-edge/webview2/",
            AppEnv.AppName,
            MB_OK | MB_ICONWARNING);
        return Task.CompletedTask;
    }

    public Task InitializeBrowserEnvironmentAsync()
    {
        var userData = Path.Combine(DataRoot, "WebView2");
        return WebView2Environment.InitializeAsync(userData);
    }

    public async Task OpenMainWindowAsync(string url, IReadOnlyList<IJsInteropDispatcher> bridgeHandlers)
    {
        var window = new BorderlessWindow(AppEnv.AppName, width: 940, height: 560);
        await window.InitializeBrowserAsync().ConfigureAwait(true);

        var bridge = new JsBridge(window.Browser);
        foreach (var h in bridgeHandlers)
            bridge.Register(h);
        bridge.InjectScript();

        window.Browser.ResizeToFill();
        window.Show();
        window.Browser.Navigate(url);

        Log.Info("Main window shown ({0} handlers registered)", bridgeHandlers.Count);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
