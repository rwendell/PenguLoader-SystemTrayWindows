using Pengu.Bridge;
using Pengu.Config;
using Pengu.Logging;
using Pengu.Native;
using Pengu.Plugins;

namespace Pengu.Api;

/// <summary>
/// Bridge surface for plugin discovery and disabled-state. Exposed as
/// <c>window.pengu.plugins</c>. Reads the typed snapshot from
/// <see cref="ConfigStore"/> for the plugins-dir / disabled-list, walks the
/// folder, and returns the flat list the hub renders.
///
/// <para>Plugin discovery + JSDoc tag parse runs entirely C#-side; the hub's
/// <c>lib/plugins.ts</c> is a thin passthrough.</para>
/// </summary>
[JsInterop("plugins")]
public partial class PluginsApi
{
    private readonly ConfigStore _config;
    private readonly PluginDiscovery _discovery;
    private readonly string _baseDir;

    public PluginsApi(ConfigStore config, string baseDir)
    {
        _config = config;
        _baseDir = baseDir;
        _discovery = new PluginDiscovery();
    }

    [JsInvokable]
    public Task<PluginInfo[]> List()
    {
        var snapshot = _config.Read();
        var pluginsDir = ResolvePluginsDir(snapshot.App.PluginsDir);
        var disabled = DisabledList.Parse(snapshot.App.DisabledPlugins);
        var list = _discovery.List(pluginsDir, disabled);
        return Task.FromResult(list.ToArray());
    }

    /// <summary>Toggle a plugin's enabled state, returning the new state.
    /// Resolves the path via discovery (so the hash matches the canonical
    /// form), updates the disabled csv, and flushes config.</summary>
    [JsInvokable]
    public Task<bool> ToggleEnabled(string path)
    {
        var snapshot = _config.Read();
        var disabled = DisabledList.Parse(snapshot.App.DisabledPlugins);

        // Path is the canonical form already (matches PluginInfo.Path), so we
        // hash it directly. lowercase + Fnv1a matches DisabledList semantics.
        var hash = Fnv1a.Hash(path.ToLowerInvariant());
        bool enabledNow;
        if (disabled.Contains(hash))
        {
            disabled.Remove(hash);
            enabledNow = true;
        }
        else
        {
            disabled.Add(hash);
            enabledNow = false;
        }

        var patched = snapshot with
        {
            App = snapshot.App with { DisabledPlugins = DisabledList.Format(disabled) },
        };
        _config.Write(patched);

        Log.Debug("Toggled plugin {0} -> enabled={1}", path, enabledNow);
        return Task.FromResult(enabledNow);
    }

    [JsInvokable]
    public Task OpenFolder()
    {
        var snapshot = _config.Read();
        var pluginsDir = ResolvePluginsDir(snapshot.App.PluginsDir);
        Shell.OpenFolder(pluginsDir);
        return Task.CompletedTask;
    }

    [JsInvokable]
    public Task RevealInFolder(string path)
    {
        var snapshot = _config.Read();
        var pluginsDir = ResolvePluginsDir(snapshot.App.PluginsDir);
        // path is canonical (forward-slashed, no leading separator); resolve
        // against pluginsDir and let Shell.RevealFile handle path normalisation.
        var full = System.IO.Path.Combine(pluginsDir, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Shell.RevealFile(full);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fetch the upstream plugin store registry. Currently a stub: the
    /// registry remains a placeholder per the design notes
    /// (no install automation planned), so we return an empty list rather
    /// than pulling in a YAML parser. When/if the registry becomes real,
    /// implement here without changing the JS surface.
    /// </summary>
    [JsInvokable]
    public Task<StorePlugin[]> FetchStoreRegistry()
    {
        return Task.FromResult(Array.Empty<StorePlugin>());
    }

    /// <summary>
    /// Resolve the on-disk plugins directory. Empty / dot-prefixed values in
    /// config mean "default": <c>&lt;DataRoot&gt;/plugins</c>.
    /// </summary>
    private string ResolvePluginsDir(string configValue)
    {
        if (string.IsNullOrWhiteSpace(configValue) || configValue.StartsWith('.'))
            return System.IO.Path.Combine(_baseDir, "plugins");
        return configValue;
    }
}
