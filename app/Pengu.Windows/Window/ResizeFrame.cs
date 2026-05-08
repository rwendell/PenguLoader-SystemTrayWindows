using System.Runtime.InteropServices;
using Pengu.Logging;
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

namespace Pengu.Windows.Window;

/// <summary>
/// Invisible top-z child window providing resize handles on a frameless
/// parent. Modeled on Tauri's <c>undecorated_resizing.rs</c> and the
/// reference project's <c>ResizeFrame.cs</c>:
///
/// <list type="number">
///   <item><description>A WS_CHILD HWND covers the parent's entire client
///     area, sitting at the top of the z-order so it overlays the WebView2
///     control.</description></item>
///   <item><description><c>SetWindowRgn</c> punches a hole in the middle so
///     mouse events on the interior fall through to whatever is beneath
///     (WebView2 / titlebar). Only the outer ~8 px frame is owned by this
///     window.</description></item>
///   <item><description><c>WM_NCHITTEST</c> returns <c>HT*</c> edge codes;
///     <c>WM_NCLBUTTONDOWN</c> is forwarded to the parent so the parent's
///     <c>DefWindowProc</c> drives the actual resize drag.</description></item>
///   <item><description>On parent <c>WM_SIZE</c> / <c>WM_DPICHANGED</c>:
///     resize this child and recompute the cutout. When the parent is
///     maximized, collapse the overlay (no resize handles needed).</description></item>
/// </list>
///
/// <para>Created after the WebView2 controller exists, then
/// <see cref="BringToTop"/> brings the overlay back above WebView2's child
/// HWND that <c>CreateCoreWebView2Controller</c> inserts.</para>
/// </summary>
internal sealed partial class ResizeFrame : IDisposable
{
    private const int HTTRANSPARENT = -1;

    private const string ClassName  = "Pengu.ResizeFrame";
    private const string WindowName = "Pengu.ResizeFrame.Window";

    private static readonly object s_classLock = new();
    private static bool s_classRegistered;
    private static readonly WindowProc s_wndProc = StaticWndProc;
    private static readonly Dictionary<HWND, ResizeFrame> s_byHwnd = new();

    private readonly HWND _parent;
    private HWND _hwnd;
    private bool _disposed;

    public ResizeFrame(HWND parent)
    {
        _parent = parent;
        EnsureClassRegistered();

        GetClientRect(parent, out var rect);
        int w = rect.right - rect.left;
        int h = rect.bottom - rect.top;

        _hwnd = CreateWindowEx(
            WindowStylesEx.WS_EX_NOACTIVATE | WindowStylesEx.WS_EX_TRANSPARENT,
            ClassName,
            WindowName,
            WindowStyles.WS_CHILD | WindowStyles.WS_VISIBLE | WindowStyles.WS_CLIPSIBLINGS,
            0, 0, w, h,
            parent,
            HMENU.NULL,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (_hwnd.IsNull)
            throw new InvalidOperationException(
                $"CreateWindowEx (resize-frame) failed: 0x{Marshal.GetLastWin32Error():x}");

        s_byHwnd[_hwnd] = this;

        UpdateCutout(w, h);
        BringToTop();
    }

    /// <summary>Show/hide the overlay. Used to disable resize handles while
    /// the host window is in real fullscreen, where the outer 8 px would be
    /// at monitor edges and clicking them would otherwise still try to
    /// resize.</summary>
    public void SetVisible(bool visible)
    {
        if (_hwnd.IsNull) return;
        ShowWindow(_hwnd, visible ? ShowWindowCommand.SW_SHOW : ShowWindowCommand.SW_HIDE);
    }

    /// <summary>Re-z-order on top — call after WebView2 is created or any
    /// time a new child might have been inserted above us.</summary>
    public void BringToTop()
    {
        if (_hwnd.IsNull) return;
        SetWindowPos(_hwnd, HWND.HWND_TOP, 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_NOMOVE |
            SetWindowPosFlags.SWP_NOOWNERZORDER | SetWindowPosFlags.SWP_NOSIZE);
    }

    public void OnParentResized()
    {
        if (_hwnd.IsNull) return;

        if (IsZoomed(_parent))
        {
            // No resize handles when maximized — collapse the overlay.
            SetWindowPos(_hwnd, HWND.HWND_TOP, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_NOMOVE |
                SetWindowPosFlags.SWP_NOOWNERZORDER);
            return;
        }

        GetClientRect(_parent, out var rect);
        int w = rect.right - rect.left;
        int h = rect.bottom - rect.top;
        SetWindowPos(_hwnd, HWND.HWND_TOP, 0, 0, w, h,
            SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_NOMOVE |
            SetWindowPosFlags.SWP_NOOWNERZORDER);
        UpdateCutout(w, h);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_hwnd.IsNull)
        {
            s_byHwnd.Remove(_hwnd);
            DestroyWindow(_hwnd);
            _hwnd = default;
        }
    }

    private void UpdateCutout(int w, int h)
    {
        var (bx, by) = GetFrameMetrics();

        // Outer rect minus inner rect = ring-shaped frame. Pattern: mutate
        // hOuter in place via RGN_DIFF, hand it to SetWindowRgn (which takes
        // ownership on success), and free hInner unconditionally.
        var hOuter = CreateRectRgn(0, 0, w, h);
        var hInner = CreateRectRgn(bx, by, w - bx, h - by);
        CombineRgn(hOuter, hOuter, hInner, RGN_DIFF);
        DeleteObject(hInner);

        if (SetWindowRgnGdi(_hwnd.DangerousGetHandle(), hOuter, true) == 0)
            DeleteObject(hOuter);
    }

    private (int bx, int by) GetFrameMetrics()
    {
        uint dpi = GetDpiForWindow(_parent);
        if (dpi == 0) dpi = 96;
        int bx = GetSystemMetricsForDpi(SystemMetric.SM_CXFRAME, dpi)
               + GetSystemMetricsForDpi(SystemMetric.SM_CXPADDEDBORDER, dpi);
        int by = GetSystemMetricsForDpi(SystemMetric.SM_CYFRAME, dpi)
               + GetSystemMetricsForDpi(SystemMetric.SM_CXPADDEDBORDER, dpi);
        return (bx, by);
    }

    private static void EnsureClassRegistered()
    {
        if (s_classRegistered) return;
        lock (s_classLock)
        {
            if (s_classRegistered) return;
            // hbrBackground = NULL because the cutout HRGN makes the visible
            // 8-px ring transparent at the OS level (no painting needed).
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = s_wndProc,
                hInstance = GetModuleHandle(null),
                hbrBackground = HBRUSH.NULL,
                hCursor = HCURSOR.NULL,
                lpszClassName = ClassName,
            };
            if (RegisterClassEx(in wc).IsInvalid)
                throw new InvalidOperationException(
                    $"RegisterClassEx (resize-frame) failed: 0x{Marshal.GetLastWin32Error():x}");
            s_classRegistered = true;
        }
    }

    private static IntPtr StaticWndProc(HWND hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (s_byHwnd.TryGetValue(hwnd, out var self))
        {
            switch ((WindowMessage)msg)
            {
                case WindowMessage.WM_NCHITTEST:     return self.OnNcHitTest(lParam);
                case WindowMessage.WM_NCLBUTTONDOWN: return self.OnNcLButtonDown(lParam);
            }
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private IntPtr OnNcHitTest(IntPtr lParam)
    {
        // Only resize if the parent has WS_THICKFRAME. Lets BorderlessWindow
        // toggle resizability by adding/removing the style without us caring.
        var style = (long)GetWindowLongPtr(_parent, WindowLongFlags.GWL_STYLE);
        if ((style & (long)WindowStyles.WS_THICKFRAME) == 0)
            return (IntPtr)HTTRANSPARENT;

        int sx = (short)((long)lParam & 0xFFFF);
        int sy = (short)(((long)lParam >> 16) & 0xFFFF);

        GetWindowRect(_hwnd, out var r);
        var (bx, by) = GetFrameMetrics();
        int code = HitEdge(r.left, r.top, r.right, r.bottom, sx, sy, bx, by);
        return (IntPtr)code;
    }

    private IntPtr OnNcLButtonDown(IntPtr lParam)
    {
        // Forward to parent so DefWindowProc on the parent drives the resize drag.
        int sx = (short)((long)lParam & 0xFFFF);
        int sy = (short)(((long)lParam >> 16) & 0xFFFF);

        GetWindowRect(_hwnd, out var r);
        var (bx, by) = GetFrameMetrics();
        int code = HitEdge(r.left, r.top, r.right, r.bottom, sx, sy, bx, by);
        if (code != HTTRANSPARENT)
        {
            int packed = (sx & 0xFFFF) | ((sy & 0xFFFF) << 16);
            PostMessage(_parent, (uint)WindowMessage.WM_NCLBUTTONDOWN, (IntPtr)code, (IntPtr)packed);
        }
        return IntPtr.Zero;
    }

    private static int HitEdge(int left, int top, int right, int bottom, int cx, int cy, int bx, int by)
    {
        int code = 0;
        if (cx < left   + bx)  code |= 0b0001;
        if (cx >= right - bx)  code |= 0b0010;
        if (cy < top    + by)  code |= 0b0100;
        if (cy >= bottom - by) code |= 0b1000;
        return code switch
        {
            0b0000 => HTTRANSPARENT,
            0b0001 => (int)HitTestValues.HTLEFT,
            0b0010 => (int)HitTestValues.HTRIGHT,
            0b0100 => (int)HitTestValues.HTTOP,
            0b1000 => (int)HitTestValues.HTBOTTOM,
            0b0101 => (int)HitTestValues.HTTOPLEFT,
            0b0110 => (int)HitTestValues.HTTOPRIGHT,
            0b1001 => (int)HitTestValues.HTBOTTOMLEFT,
            0b1010 => (int)HitTestValues.HTBOTTOMRIGHT,
            _      => HTTRANSPARENT,
        };
    }

    // ---------- GDI / region P/Invokes ----------
    // Inlined here instead of pulling Vanara.PInvoke.Gdi32 — the surface we
    // need is tiny (5 functions) and AOT-clean via [LibraryImport].

    private const int RGN_DIFF = 4;

    [LibraryImport("gdi32.dll", EntryPoint = "CreateRectRgn")]
    private static partial IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll", EntryPoint = "CombineRgn")]
    private static partial int CombineRgn(IntPtr hRgnDest, IntPtr hRgnSrc1, IntPtr hRgnSrc2, int mode);

    [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    // Vanara's SetWindowRgn signature uses HRGN/HWND wrappers we don't have
    // (no Vanara.PInvoke.Gdi32 dep). Using the raw user32 export directly.
    [LibraryImport("user32.dll", EntryPoint = "SetWindowRgn")]
    private static partial int SetWindowRgnGdi(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);
}
