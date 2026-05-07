using System.Text;
using System.Text.Json;
using Pengu.Logging;

namespace Pengu.DataStore;

/// <summary>
/// Read-only view of <c>&lt;data_root&gt;/datastore</c>, the XOR-encoded JSON
/// file the C++ core's in-LCUX <c>window.DataStore</c> reads / writes. The
/// hub's Settings → Data tab uses this to *browse* contents; plugin writes
/// still flow through the core's API inside LCUX, never via the hub.
///
/// <para>The XOR is documented as a "speed bump, not a security boundary"
/// (see <c>docs/design.md</c> §9). Same key (<c>A5dgY6lz9fpG9kGNiH1mZ</c>)
/// the core uses, mirrored here so reads are byte-for-byte equivalent.</para>
///
/// <para>Failures (missing file, malformed JSON, decode mismatch) return an
/// empty object — same convention as the v1.1.6/Tauri JS implementation.</para>
/// </summary>
public sealed class DataStoreReader
{
    private const string XorKey = "A5dgY6lz9fpG9kGNiH1mZ";
    private readonly string _path;

    public DataStoreReader(string dataRoot)
    {
        _path = Path.Combine(dataRoot, "datastore");
    }

    /// <summary>
    /// Read + decode + parse the datastore. Returns an empty JSON object
    /// (<c>{}</c>) on any failure — caller doesn't need to distinguish
    /// missing-file from malformed-bytes.
    /// </summary>
    public JsonElement Read()
    {
        try
        {
            if (!File.Exists(_path))
                return EmptyObject();

            var bytes = File.ReadAllBytes(_path);
            if (bytes.Length < 2)
                return EmptyObject();

            // In-place XOR with the cycling key.
            var keyBytes = Encoding.UTF8.GetBytes(XorKey);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= keyBytes[i % keyBytes.Length];

            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            // Clone so the JsonElement survives JsonDocument disposal.
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            Log.Warn("DataStoreReader: failed to read {0} ({1}); returning empty", _path, ex.Message);
            return EmptyObject();
        }
    }

    private static JsonElement EmptyObject()
    {
        // Pre-parsed empty object literal; cheap and lets us return a valid
        // JsonElement on the failure path without throwing.
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}
