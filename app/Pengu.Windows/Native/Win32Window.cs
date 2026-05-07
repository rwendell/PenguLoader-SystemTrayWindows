using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

namespace Pengu.Windows.Native;

/// <summary>
/// Base class for HWND-backed windows. Handles class registration once per
/// subclass, GCHandle threading so static <see cref="WindowProc"/> can route
/// to the right instance, and the standard <see cref="WndProc"/> dispatch
/// shape Win32 expects.
///
/// <para>Subclasses override <see cref="WndProc"/> and call <see cref="Create"/>
/// from their constructor with the desired styles + initial size.</para>
/// </summary>
public abstract class Win32Window
{
    // One class per subclass, registered lazily on first instantiation.
    private static readonly Dictionary<Type, string> s_classNames = new();
    private static readonly object s_classLock = new();
    // Anchored so the unmanaged side can't GC the function pointer while a
    // class is registered.
    private static readonly WindowProc s_wndProcDelegate = StaticWndProc;

    private GCHandle _selfHandle;
    public HWND Handle { get; private set; }

    protected Win32Window() { }

    /// <summary>Create the underlying HWND. Called by subclasses from their
    /// constructor after they've prepared whatever state <see cref="WndProc"/>
    /// will need on early messages (WM_NCCREATE / WM_CREATE arrive synchronously).</summary>
    protected void Create(string title, int x, int y, int width, int height, WindowStyles style)
    {
        var className = EnsureClassRegistered(GetType());
        _selfHandle = GCHandle.Alloc(this);

        Handle = CreateWindowEx(
            WindowStylesEx.WS_EX_APPWINDOW,
            className, title, style,
            x, y, width, height,
            HWND.NULL,
            HMENU.NULL,
            GetModuleHandle(null),
            (IntPtr)_selfHandle);

        if (Handle.IsNull)
        {
            _selfHandle.Free();
            throw new InvalidOperationException(
                $"CreateWindowEx failed (Win32 error {Marshal.GetLastWin32Error():x}).");
        }
    }

    public void Show() => ShowWindow(Handle, ShowWindowCommand.SW_SHOW);
    public void Hide() => ShowWindow(Handle, ShowWindowCommand.SW_HIDE);
    public void Close() => PostMessage(Handle, (uint)WindowMessage.WM_CLOSE);

    public void Destroy()
    {
        if (!Handle.IsNull)
            DestroyWindow(Handle);
    }

    /// <summary>Window procedure. Subclasses override this and call
    /// <see cref="DefaultProc"/> for messages they don't handle.</summary>
    protected virtual IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
        => DefaultProc(msg, wParam, lParam);

    protected IntPtr DefaultProc(uint msg, IntPtr wParam, IntPtr lParam)
        => DefWindowProc(Handle, msg, wParam, lParam);

    private static string EnsureClassRegistered(Type windowType)
    {
        lock (s_classLock)
        {
            if (s_classNames.TryGetValue(windowType, out var existing))
                return existing;

            var className = $"Pengu.{windowType.Name}";
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = WindowClassStyles.CS_HREDRAW | WindowClassStyles.CS_VREDRAW,
                lpfnWndProc = s_wndProcDelegate,
                hInstance = GetModuleHandle(null),
                hCursor = LoadCursor(HINSTANCE.NULL, Macros.MAKEINTRESOURCE(32512)), // IDC_ARROW
                hbrBackground = (IntPtr)1, // COLOR_WINDOW + 1; we paint our own bg in WM_PAINT for the borderless window.
                // Embedded IDI_APPLICATION icon (from <ApplicationIcon> in the csproj).
                // Setting on the class means every window of this class gets the icon
                // on the taskbar / Alt+Tab / system menu without per-instance WM_SETICON.
                hIcon = AppIcon.Get(),
                hIconSm = AppIcon.GetSmall(),
                lpszClassName = className,
            };

            if (RegisterClassEx(in wc).IsInvalid)
                throw new InvalidOperationException(
                    $"RegisterClassEx failed (Win32 error {Marshal.GetLastWin32Error():x}).");

            s_classNames[windowType] = className;
            return className;
        }
    }

    private static IntPtr StaticWndProc(HWND hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Win32Window? self = null;

        if (msg == (uint)WindowMessage.WM_NCCREATE)
        {
            // CREATESTRUCT.lpCreateParams is the GCHandle we passed to CreateWindowEx.
            // Stash it in GWLP_USERDATA so subsequent messages can recover it.
            unsafe
            {
                var cs = (CREATESTRUCT*)lParam;
                if (cs->lpCreateParams != IntPtr.Zero)
                {
                    self = GCHandle.FromIntPtr(cs->lpCreateParams).Target as Win32Window;
                    if (self is not null)
                    {
                        self.Handle = hwnd;
                        SetWindowLong(hwnd, WindowLongFlags.GWLP_USERDATA, cs->lpCreateParams);
                    }
                }
            }
        }
        else
        {
            var ud = GetWindowLongPtr(hwnd, WindowLongFlags.GWLP_USERDATA);
            if (ud != IntPtr.Zero)
                self = GCHandle.FromIntPtr(ud).Target as Win32Window;
        }

        if (self is not null)
        {
            try
            {
                var result = self.WndProc(msg, wParam, lParam);
                if (msg == (uint)WindowMessage.WM_NCDESTROY)
                {
                    if (self._selfHandle.IsAllocated)
                        self._selfHandle.Free();
                }
                return result;
            }
            catch (Exception ex)
            {
                Pengu.Logging.Log.Error(ex, "WndProc threw on msg={0:x4}", msg);
                return DefWindowProc(hwnd, msg, wParam, lParam);
            }
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CREATESTRUCT
    {
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy, cx, y, x;
        public int style;
        public IntPtr lpszName;
        public IntPtr lpszClass;
        public uint dwExStyle;
    }
}
