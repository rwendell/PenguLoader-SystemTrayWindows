using System.Text.Json.Serialization;

namespace Pengu.Plugins;

/// <summary>
/// One plugin discovered on disk, with disabled-state already resolved
/// against the <c>disabled_plugins</c> config csv.
///
/// <para><see cref="Path"/> is the canonical relative path used by the
/// <c>https://plugins/</c> scheme handler in the C++ core. Forward-slashed,
/// excluding the <c>plugins_dir</c> prefix, with any v1.1.6-era <c>_</c>
/// disable suffix stripped.</para>
///
/// <para><see cref="Hash"/> is the FNV-1a 32-bit hash of
/// <c>Path.ToLowerInvariant()</c>. Same convention the disabled-list csv
/// uses (lowercase hex, no prefix).</para>
/// </summary>
public sealed record PluginInfo(
    string Name,
    string Path,
    uint Hash,
    string? Author,
    string? Description,
    string? Link,
    bool Enabled);

/// <summary>One entry in the upstream plugin store registry. Mirrors the
/// shape of <c>registry/plugins.yml</c> in the
/// <see href="https://github.com/PenguLoader/plugin-store">plugin-store</see>
/// repo.</summary>
public sealed record StorePlugin(
    string Name,
    string Slug,
    string Description,
    string[] Images,
    string Repo,
    string[] Tags,
    [property: JsonPropertyName("theme")] bool Theme,
    [property: JsonPropertyName("auto_update")] bool AutoUpdate);
