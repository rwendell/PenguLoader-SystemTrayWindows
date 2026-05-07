using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace Pengu.Windows.Native;

/// <summary>
/// Thin wrappers for the DWM attributes we use on the borderless window:
/// dark titlebar (cosmetic; titlebar is hidden but the 1px frame still
/// reflects this), client-area shadow margin (so the borderless window
/// gets a real Win32 drop shadow), and rounded corners on Win11.
/// </summary>
internal static partial class Dwm
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int left, right, top, bottom; }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hwnd, in MARGINS pMarInset);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, in int pvAttribute, int cbAttribute);

    /// <summary>Tell DWM to render the (hidden) titlebar in dark theme.
    /// Cosmetic — the borderless window has no titlebar — but eliminates a
    /// 1-pixel light edge that appears on the top of the window otherwise.</summary>
    public static void EnableDarkTitleBar(HWND hwnd)
    {
        int dark = 1;
        DwmSetWindowAttribute(hwnd.DangerousGetHandle(), DWMWA_USE_IMMERSIVE_DARK_MODE, in dark, sizeof(int));
    }

    /// <summary>Extend the frame 1px into the client area so DWM paints a
    /// drop shadow on the borderless window. Without this the window is
    /// shadowless / floating-with-no-elevation.</summary>
    public static void EnableShadowFrameless(HWND hwnd)
    {
        var margins = new MARGINS { top = 1 };
        DwmExtendFrameIntoClientArea(hwnd.DangerousGetHandle(), in margins);
    }

    /// <summary>Round corners on Win11. No-op on Win10 (the attribute is
    /// silently ignored).</summary>
    public static void RoundCorners(HWND hwnd)
    {
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd.DangerousGetHandle(), DWMWA_WINDOW_CORNER_PREFERENCE, in pref, sizeof(int));
    }
}
