namespace Pengu.Api;

/// <summary>
/// Snapshot of the host process environment exposed to the hub via
/// <c>pengu.host.getInfo()</c>. Synchronous on the C# side; async on the JS
/// side because every bridge method is async.
/// </summary>
public sealed record HostInfo(
    string Os,
    string Version,
    string Build,
    bool IsMac,
    bool IsAdmin,
    string Locale);

/// <summary>
/// One entry returned from <c>pengu.fs.readDir(path)</c>.
/// </summary>
public sealed record DirEntry(string Name, bool IsDir);
