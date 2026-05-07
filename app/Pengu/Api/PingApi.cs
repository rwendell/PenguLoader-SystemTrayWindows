using Pengu.Bridge;

namespace Pengu.Api;

/// <summary>
/// Smoke-test API used to validate the bridge round-trip end-to-end. Exposed
/// as <c>window.pengu.ping</c>.
///
/// <para>Once the real APIs (activation / config / plugins / host / i18n)
/// land, this stays in place as a deliberate diagnostic — it's the smallest
/// possible bridge call, useful for "is the bridge alive" checks in dev tools.</para>
/// </summary>
[JsInterop("ping")]
public partial class PingApi
{
    [JsInvokable]
    public Task<string> Echo(string message) => Task.FromResult(message);

    [JsInvokable]
    public Task<int> Add(int a, int b) => Task.FromResult(a + b);

    [JsInvokable]
    public Task<string> Version() => Task.FromResult(AppEnv.AppVersion);
}
