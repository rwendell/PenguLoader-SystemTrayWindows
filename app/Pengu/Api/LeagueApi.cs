using Pengu.Bridge;
using Pengu.League;

namespace Pengu.Api;

/// <summary>
/// League of Legends discovery / validation. Exposed as
/// <c>window.pengu.league</c>. Both methods are pure file IO + path math;
/// no platform branching needed beyond what <see cref="LeagueDiscovery"/>
/// already does (returns null on macOS).
/// </summary>
[JsInterop("league")]
public partial class LeagueApi
{
    [JsInvokable]
    public Task<string?> FindInstall() => Task.FromResult(LeagueDiscovery.FindInstall());

    [JsInvokable]
    public Task<bool> ValidateDir(string dir) => Task.FromResult(LeagueDiscovery.ValidateDir(dir));
}
