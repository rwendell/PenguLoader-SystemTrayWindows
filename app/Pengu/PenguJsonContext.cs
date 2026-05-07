using System.Text.Json.Serialization;
using Pengu.Activation;
using Pengu.Config;

namespace Pengu;

/// <summary>
/// AOT JSON serializer source-generation context. Every type that crosses the
/// bridge boundary in either direction needs a <see cref="JsonSerializableAttribute"/>
/// entry here so the source generator emits a reflection-free serializer for
/// it. The bridge dispatcher (emitted by Pengu.Gen) routes through this
/// context for all primitive and record (de)serialization.
///
/// <para>Naming: global default is <see cref="JsonKnownNamingPolicy.CamelCase"/>.
/// Records whose property names mirror an external file format (e.g.
/// <see cref="ConfigApp"/> matching ini key names) override per-property via
/// <see cref="JsonPropertyNameAttribute"/>.</para>
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
[JsonSerializable(typeof(ActivationMode))]
[JsonSerializable(typeof(ConfigSnapshot))]
[JsonSerializable(typeof(ConfigApp))]
[JsonSerializable(typeof(ConfigClient))]
public partial class PenguJsonContext : JsonSerializerContext;
