using System.Runtime.InteropServices;
using Pengu.Logging;

namespace Pengu.Windows.Native;

/// <summary>
/// Native folder picker via <c>SHBrowseForFolderW</c>. Older API than
/// <c>IFileOpenDialog</c> but considerably simpler (no COM dance), still
/// shipped in shell32.dll, and AOT-clean with two <c>[LibraryImport]</c>s.
///
/// <para>For Pengu's use cases (picking the plugins folder, the LoL install
/// folder) the dialog is fine — users see it rarely. If a modern look is
/// wanted later we can swap in an <c>IFileOpenDialog</c> wrapper without
/// touching call sites.</para>
/// </summary>
public static partial class FolderPicker
{
    private const int BIF_RETURNONLYFSDIRS = 0x0001;
    private const int BIF_NEWDIALOGSTYLE   = 0x0040;
    private const int BIF_USENEWUI         = BIF_NEWDIALOGSTYLE | 0x0010; // BIF_EDITBOX | BIF_NEWDIALOGSTYLE

    private const int BFFM_INITIALIZED = 1;
    private const int BFFM_SETSELECTIONW = 0x400 + 103; // WM_USER + 103

    // All-blittable struct so [LibraryImport] source-gen accepts it. The
    // title is an IntPtr to a CoTaskMem-allocated UTF-16 buffer (allocated
    // and freed by Pick).
    [StructLayout(LayoutKind.Sequential)]
    private struct BROWSEINFOW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public IntPtr lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    private delegate int BrowseCallbackProc(IntPtr hwnd, uint uMsg, IntPtr lParam, IntPtr lpData);

    /// <summary>
    /// Show the folder picker. Returns the selected path or null if the user
    /// cancelled.
    /// </summary>
    /// <param name="ownerHwnd">Parent HWND for modality (or zero).</param>
    /// <param name="title">Dialog caption / instructional text.</param>
    /// <param name="initialPath">Starting folder; null to use shell default.</param>
    public static string? Pick(IntPtr ownerHwnd, string title, string? initialPath)
    {
        // Initial-path delivery: the dialog uses a callback to receive the
        // initial selection because BROWSEINFO doesn't have a direct field
        // for it. We pin the path string and post BFFM_SETSELECTIONW from
        // BFFM_INITIALIZED.
        IntPtr initialPathPtr = IntPtr.Zero;
        IntPtr titlePtr = IntPtr.Zero;
        IntPtr displayBuf = IntPtr.Zero;
        BrowseCallbackProc? callback = null;
        try
        {
            if (!string.IsNullOrEmpty(initialPath))
            {
                initialPathPtr = Marshal.StringToCoTaskMemUni(initialPath);
                callback = static (hwnd, msg, lParam, lpData) =>
                {
                    if (msg == BFFM_INITIALIZED && lpData != IntPtr.Zero)
                        SendMessageW(hwnd, BFFM_SETSELECTIONW, (IntPtr)1, lpData);
                    return 0;
                };
            }

            titlePtr = Marshal.StringToCoTaskMemUni(title);
            displayBuf = Marshal.AllocCoTaskMem(260 * 2); // MAX_PATH wchars
            try
            {
                var bi = new BROWSEINFOW
                {
                    hwndOwner = ownerHwnd,
                    pidlRoot = IntPtr.Zero,
                    pszDisplayName = displayBuf,
                    lpszTitle = titlePtr,
                    ulFlags = BIF_RETURNONLYFSDIRS | BIF_USENEWUI,
                    lpfn = callback is null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate(callback),
                    lParam = initialPathPtr,
                    iImage = 0,
                };

                IntPtr pidl = SHBrowseForFolderW(ref bi);
                if (pidl == IntPtr.Zero) return null;

                try
                {
                    Span<char> path = stackalloc char[260];
                    unsafe
                    {
                        fixed (char* p = path)
                        {
                            return SHGetPathFromIDListW(pidl, (IntPtr)p) ? new string(p) : null;
                        }
                    }
                }
                finally
                {
                    CoTaskMemFree(pidl);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(displayBuf);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("FolderPicker.Pick failed: {0}", ex.Message);
            return null;
        }
        finally
        {
            if (initialPathPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(initialPathPtr);
            if (titlePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(titlePtr);
            GC.KeepAlive(callback); // anchor through the call
        }
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHBrowseForFolderW")]
    private static partial IntPtr SHBrowseForFolderW(ref BROWSEINFOW lpbi);

    [LibraryImport("shell32.dll", EntryPoint = "SHGetPathFromIDListW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SHGetPathFromIDListW(IntPtr pidl, IntPtr pszPath);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessageW(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(IntPtr ptr);
}
