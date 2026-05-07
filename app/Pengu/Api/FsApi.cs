using Pengu.Bridge;
using Pengu.Logging;

namespace Pengu.Api;

/// <summary>
/// Minimal file-system escape hatches exposed as <c>window.pengu.fs</c>.
/// Domain-specific concerns (config, plugins, datastore) have typed APIs of
/// their own — this surface is for niche needs that don't justify a
/// dedicated method. No path validation: matches the original Tauri
/// allowlist's <c>scope: ['**']</c>.
///
/// <para>Failures are surfaced as bridge errors (the dispatcher catches and
/// replies with <c>{ok: false, error}</c>), so callers see typed Promise
/// rejections instead of opaque crashes.</para>
/// </summary>
[JsInterop("fs")]
public partial class FsApi
{
    [JsInvokable]
    public async Task<string> ReadText(string path)
        => await File.ReadAllTextAsync(path).ConfigureAwait(false);

    [JsInvokable]
    public async Task WriteText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
    }

    [JsInvokable]
    public Task<bool> Exists(string path)
        => Task.FromResult(File.Exists(path) || Directory.Exists(path));

    [JsInvokable]
    public Task<DirEntry[]> ReadDir(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return Task.FromResult(Array.Empty<DirEntry>());

            var entries = new List<DirEntry>();
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name)) continue;
                entries.Add(new DirEntry(name, Directory.Exists(entry)));
            }
            return Task.FromResult(entries.ToArray());
        }
        catch (Exception ex)
        {
            Log.Warn("FsApi.ReadDir failed for {0}: {1}", path, ex.Message);
            return Task.FromResult(Array.Empty<DirEntry>());
        }
    }
}
