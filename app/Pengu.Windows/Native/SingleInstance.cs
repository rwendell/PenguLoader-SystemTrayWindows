using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace Pengu.Windows.Native;

/// <summary>
/// Single-instance lock + second-instance broadcast. Same UUID and
/// <c>RegisterWindowMessage</c> name as v1.1.6 so the new and old binaries
/// treat each other as "already running" during the migration window.
///
/// <para>Pattern (per v1.1.6): first instance owns a named Mutex, second
/// instance fails to acquire it, broadcasts <c>WM_SHOWME</c> via
/// <c>PostMessage(HWND_BROADCAST, ...)</c>, and exits. The first instance's
/// main window WndProc handles the message by restoring + foregrounding
/// itself. No extra kernel objects (named events, pipes) needed.</para>
/// </summary>
public static partial class SingleInstance
{
    private const int HWND_BROADCAST = 0xFFFF;

    /// <summary>
    /// System-unique window message ID for our broadcast. Same string the
    /// v1.1.6 WPF loader uses; cross-version compatibility hinges on the
    /// string matching exactly.
    /// </summary>
    public static readonly uint WM_SHOWME = (uint)RegisterWindowMessageW(AppEnv.AppName);

    private static Mutex? s_mutex;

    /// <summary>
    /// Try to acquire the single-instance mutex. Returns true if this is the
    /// first instance; false if another is already running (and we've
    /// broadcast the show-me message to it).
    /// </summary>
    public static bool TryAcquire()
    {
        s_mutex = new Mutex(initiallyOwned: true, AppEnv.SingleInstanceMutex, out var createdNew);
        if (createdNew) return true;

        // Another instance is running — wake it.
        PostMessageW((IntPtr)HWND_BROADCAST, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
        s_mutex.Dispose();
        s_mutex = null;
        return false;
    }

    public static void Release()
    {
        s_mutex?.ReleaseMutex();
        s_mutex?.Dispose();
        s_mutex = null;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterWindowMessageW(string lpString);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
