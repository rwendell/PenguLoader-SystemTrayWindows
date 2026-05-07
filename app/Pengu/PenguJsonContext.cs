using System.Text.Json.Serialization;

namespace Pengu;

/// <summary>
/// AOT JSON serializer source-generation context. Every type that crosses the
/// bridge boundary in either direction needs a <see cref="JsonSerializableAttribute"/>
/// entry here so the source generator emits a reflection-free serializer for
/// it. The bridge dispatcher (emitted by Pengu.Gen) routes through this
/// context for all primitive and record (de)serialization.
///
/// <para>Add new types alongside the API surface they belong to. Records that
/// only ever appear as method return types still need an entry; same for
/// argument records.</para>
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(byte))]
public partial class PenguJsonContext : JsonSerializerContext;
