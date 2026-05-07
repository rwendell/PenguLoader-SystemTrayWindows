using System.Text;

namespace Pengu.Plugins;

/// <summary>
/// 32-bit FNV-1a hash. Matches the algorithm in the hub's pre-port
/// <c>lib/utils.ts</c> <c>getHash</c> and the C++ core's renderer-side
/// disabled-plugin filter so hashes round-trip across all three components
/// (host, hub, core).
/// </summary>
public static class Fnv1a
{
    private const uint Offset = 0x811c9dc5;
    private const uint Prime  = 0x01000193;

    public static uint Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        uint hash = Offset;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= Prime;
        }
        return hash;
    }
}
