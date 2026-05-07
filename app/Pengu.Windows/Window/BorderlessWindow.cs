using Pengu.Windows.Browser;
using Pengu.Windows.Native;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace Pengu.Windows.Window;

/// <summary>
/// Frameless Win32 window that hosts a single WebView2 filling the client
/// area. The SolidJS hub paints all visible chrome (titlebar buttons,
/// drag region) inside the page; this class only:
///
/// <list type="bullet">
///   <item><description>Removes the standard non-client area via <c>WM_NCCALCSIZE</c>.</description></item>
///   <item><description>Returns <c>HTCLIENT</c> for everything in <c>WM_NCHITTEST</c> (drag is via <c>app-region: drag</c> CSS).</description></item>
///   <item><description>Applies DWM dark titlebar / shadow / rounded corners.</description></item>
///   <item><description>Resizes the WebView2 on <c>WM_SIZE</c> / <c>WM_DPICHANGED</c>.</description></item>
///   <item><description>Handles <c>WM_SHOWME</c> from <see cref="SingleInstance"/> by restoring + foregrounding.</description></item>
/// </list>
///
/// The hub is non-resizable today; <c>WS_THICKFRAME</c> / <c>WS_MAXIMIZEBOX</c>
/// are stripped. Resize-frame overlay deliberately omitted.
/// </summary>
public sealed class BorderlessWindow : Win32Window
{
    private readonly Browser.Browser _browser;
    public Browser.Browser Browser => _browser;

    public BorderlessWindow(string title, int width, int height)
    {
        // WS_OVERLAPPEDWINDOW gives us the standard window-list-and-taskbar
        // hookup; WM_NCCALCSIZE then removes the visible non-client area.
        // Drop WS_THICKFRAME (no resize) and WS_MAXIMIZEBOX (hub UI is fixed-size).
        var style = WindowStyles.WS_OVERLAPPEDWINDOW
                  | WindowStyles.WS_CLIPCHILDREN;
        style &= ~(WindowStyles.WS_THICKFRAME | WindowStyles.WS_MAXIMIZEBOX);

        var (x, y) = CenterOnPrimary(width, height);
        Create(title, x, y, width, height, style);

        _browser = new Browser.Browser(Handle);
    }

    public Task InitializeBrowserAsync()
    {
        // Fully qualified: the Browser property name shadows the Browser
        // namespace in this file's scope.
        var env = Pengu.Windows.Browser.WebView2Environment.Instance.Native;
        return _browser.InitializeAsync(env);
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Single-instance broadcast — focus this window if any other Pengu
        // instance posts WM_SHOWME.
        if (msg == SingleInstance.WM_SHOWME)
        {
            if (IsIconic(Handle))
                ShowWindow(Handle, ShowWindowCommand.SW_RESTORE);
            else
                ShowWindow(Handle, ShowWindowCommand.SW_SHOW);
            SetForegroundWindow(Handle);
            return IntPtr.Zero;
        }

        switch ((WindowMessage)msg)
        {
            case WindowMessage.WM_CREATE:
                Dwm.EnableDarkTitleBar(Handle);
                Dwm.EnableShadowFrameless(Handle);
                Dwm.RoundCorners(Handle);
                // Force WM_NCCALCSIZE to fire with the new frame so client area expands.
                SetWindowPos(Handle, HWND.NULL, 0, 0, 0, 0,
                    SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOMOVE |
                    SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER |
                    SetWindowPosFlags.SWP_NOACTIVATE);
                return IntPtr.Zero;

            case WindowMessage.WM_NCCALCSIZE:
                if (wParam != IntPtr.Zero)
                {
                    // Accept rgrc[0] as-is so the client rect spans the whole window.
                    return IntPtr.Zero;
                }
                break;

            case WindowMessage.WM_NCHITTEST:
                // Everything is client area; drag is via app-region:drag CSS in the page.
                return (IntPtr)(int)HitTestValues.HTCLIENT;

            case WindowMessage.WM_SIZE:
            case WindowMessage.WM_DPICHANGED:
                _browser?.ResizeToFill();
                break;

            case WindowMessage.WM_DESTROY:
                _browser?.Close();
                Dispatcher.UIThread.Exit(0);
                return IntPtr.Zero;
        }

        return DefaultProc(msg, wParam, lParam);
    }

    private static (int x, int y) CenterOnPrimary(int w, int h)
    {
        unsafe
        {
            RECT work;
            if (!SystemParametersInfo(SPI.SPI_GETWORKAREA, 0, (IntPtr)(&work), 0))
                return (unchecked((int)0x80000000), unchecked((int)0x80000000)); // CW_USEDEFAULT
            int areaW = work.right - work.left;
            int areaH = work.bottom - work.top;
            int x = work.left + Math.Max(0, (areaW - w) / 2);
            int y = work.top  + Math.Max(0, (areaH - h) / 2);
            return (x, y);
        }
    }
}
