using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

namespace Pengu.Windows.Native;

/// <summary>
/// The host process's primary icon. <see cref="Get"/> returns the standard
/// 32x32 application icon; <see cref="GetSmall"/> returns a tray-sized
/// (system small icon metric) variant from the same icon group.
///
/// <para>The .NET tooling embeds the icon declared by <c>&lt;ApplicationIcon&gt;</c>
/// in the csproj as resource <c>IDI_APPLICATION</c> (32512) in the exe.
/// Calling <c>LoadIcon(hInstance, MAKEINTRESOURCE(32512))</c> with our own
/// module handle returns that embedded icon; if no application icon was set,
/// Windows falls back to the system default. Either way we get something
/// sensible, AOT-clean, with one user32 call.</para>
/// </summary>
internal static partial class AppIcon
{
    private const int IDI_APPLICATION = 32512;
    private const uint IMAGE_ICON = 1;
    private const uint LR_DEFAULTCOLOR = 0x00000000;

    private static HICON s_icon;
    private static bool s_loaded;

    private static HICON s_smallIcon;
    private static bool s_smallLoaded;

    /// <summary>Standard 32x32 application icon.</summary>
    public static HICON Get()
    {
        if (s_loaded) return s_icon;
        s_loaded = true;
        s_icon = LoadIcon(GetModuleHandle(null), Macros.MAKEINTRESOURCE(IDI_APPLICATION));
        return s_icon;
    }

    /// <summary>
    /// Small icon at the system small-icon metric (typically 16x16).
    /// <c>LoadIcon</c> always returns 32x32; for tray / Alt+Tab small frames
    /// we want the matching frame from the icon group resource so the shell
    /// doesn't downscale a 32x32 with poor filtering.
    /// </summary>
    public static HICON GetSmall()
    {
        if (s_smallLoaded) return s_smallIcon;
        s_smallLoaded = true;
        int cx = GetSystemMetrics(SystemMetric.SM_CXSMICON);
        int cy = GetSystemMetrics(SystemMetric.SM_CYSMICON);
        var h = LoadImage(
            GetModuleHandle(null).DangerousGetHandle(),
            Macros.MAKEINTRESOURCE(IDI_APPLICATION),
            IMAGE_ICON, cx, cy, LR_DEFAULTCOLOR);
        s_smallIcon = h == IntPtr.Zero ? Get() : (HICON)h;
        return s_smallIcon;
    }

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
    private static partial IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);
}
