using System.Globalization;
using Pengu.Bridge;

namespace Pengu.Api;

/// <summary>
/// Locale information surfaced to the hub. Translations live in the bundle
/// (<c>packages/hub/translations.json</c>) and are loaded TS-side; the host
/// only tells the UI which locale to default to.
/// </summary>
[JsInterop("i18n")]
public partial class I18nApi
{
    [JsInvokable]
    public Task<string> GetSystemLocale() => Task.FromResult(CultureInfo.CurrentUICulture.Name);
}
