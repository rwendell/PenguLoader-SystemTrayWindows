using Pengu.Bridge;
using Pengu.Config;

namespace Pengu.Api;

/// <summary>
/// Bridge surface for the <c>config</c> file. Exposed as <c>window.pengu.config</c>.
///
/// <para>The hub's <c>lib/config.ts</c> mirrors <see cref="Read"/> into a
/// signal-driven cache and writes back via <see cref="Write"/> on every
/// setter. Round-trip preservation of unmodeled keys (e.g. the C++ core's
/// <c>debug_port</c>) is handled inside <see cref="ConfigStore"/>.</para>
/// </summary>
[JsInterop("config")]
public partial class ConfigApi
{
    private readonly ConfigStore _store;

    public ConfigApi(ConfigStore store) => _store = store;

    [JsInvokable]
    public Task<string> GetRoot() => Task.FromResult(_store.Directory);

    [JsInvokable]
    public Task<string> GetPath() => Task.FromResult(_store.Path);

    [JsInvokable]
    public Task<ConfigSnapshot> Read() => Task.FromResult(_store.Read());

    [JsInvokable]
    public Task Write(ConfigSnapshot patch)
    {
        _store.Write(patch);
        return Task.CompletedTask;
    }
}
