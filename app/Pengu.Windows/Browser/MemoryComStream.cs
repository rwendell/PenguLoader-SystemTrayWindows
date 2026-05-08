using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Diga.WebView2.Interop;

namespace Pengu.Windows.Browser;

/// <summary>
/// Read-only <see cref="IStream"/> backed by a managed byte array. WebView2
/// hands this to <c>CreateWebResourceResponse</c> as the response body for
/// <c>app://</c> requests served out of <see cref="Pengu.Pack.AppDat"/>.
///
/// <para>We implement only Read, Seek, and Stat — Write returns
/// <c>STG_E_ACCESSDENIED</c>; everything else returns <c>E_NOTIMPL</c>.
/// In practice WebView2 only calls Read + Seek on the response stream
/// because we set <c>Content-Length</c> in the response header.</para>
/// </summary>
[GeneratedComClass]
internal sealed partial class MemoryComStream : IStream
{
    private const int S_OK              = 0;
    private const int E_NOTIMPL         = unchecked((int)0x80004001);
    private const int E_INVALIDARG      = unchecked((int)0x80070057);
    private const int STG_E_ACCESSDENIED = unchecked((int)0x80030005);

    private readonly byte[] _data;
    private long _pos;

    public MemoryComStream(byte[] data) => _data = data;

    public int Read(IntPtr pv, ulong cb, IntPtr pcbRead)
    {
        if (pv == IntPtr.Zero) return E_INVALIDARG;
        long avail = Math.Max(0, _data.Length - _pos);
        int toRead = (int)Math.Min((long)cb, avail);
        if (toRead > 0)
        {
            Marshal.Copy(_data, (int)_pos, pv, toRead);
            _pos += toRead;
        }
        if (pcbRead != IntPtr.Zero)
            Marshal.WriteInt32(pcbRead, toRead);
        return S_OK;
    }

    public int Write(IntPtr pv, ulong cb, IntPtr pcbWritten)
    {
        if (pcbWritten != IntPtr.Zero) Marshal.WriteInt32(pcbWritten, 0);
        return STG_E_ACCESSDENIED;
    }

    public int Seek(long dlibMove, IntPtr dwOrigin, IntPtr plibNewPosition)
    {
        // STREAM_SEEK_SET=0, STREAM_SEEK_CUR=1, STREAM_SEEK_END=2.
        long origin = dwOrigin.ToInt64();
        long newPos = origin switch
        {
            0 => dlibMove,
            1 => _pos + dlibMove,
            2 => _data.Length + dlibMove,
            _ => -1,
        };
        if (newPos < 0) return E_INVALIDARG;
        _pos = newPos;
        if (plibNewPosition != IntPtr.Zero)
            Marshal.WriteInt64(plibNewPosition, _pos);
        return S_OK;
    }

    public int SetSize(ulong libNewSize) => E_NOTIMPL;

    public int CopyTo(ref IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        => E_NOTIMPL;

    public int Commit(uint grfCommitFlags) => S_OK;
    public int Revert() => E_NOTIMPL;
    public int LockRegion(long libOffset, long cb, uint dwLockType) => E_NOTIMPL;
    public int UnlockRegion(long libOffset, long cb, uint dwLockType) => E_NOTIMPL;

    public int Stat(IntPtr pstatstg, uint grfStatFlag)
    {
        // STATSTG (Win64): pwcsName(8), type(4)+pad(4), cbSize(8), ... — write the
        // ones consumers care about and zero everything else we touch. WebView2
        // doesn't usually call Stat when Content-Length is in the response header.
        if (pstatstg == IntPtr.Zero) return E_INVALIDARG;
        Marshal.WriteIntPtr(pstatstg, IntPtr.Zero);                     // pwcsName = null
        Marshal.WriteInt32(pstatstg + IntPtr.Size, 1);                  // type = STGTY_STREAM
        Marshal.WriteInt32(pstatstg + IntPtr.Size + 4, 0);              // padding
        Marshal.WriteInt64(pstatstg + IntPtr.Size + 8, _data.Length);   // cbSize
        return S_OK;
    }

    public int Clone(out IStream ppstm)
    {
        ppstm = null!;
        return E_NOTIMPL;
    }
}
