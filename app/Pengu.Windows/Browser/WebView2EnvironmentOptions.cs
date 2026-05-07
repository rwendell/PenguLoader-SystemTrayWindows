using System.Runtime.InteropServices.Marshalling;
using Diga.WebView2.Interop;

namespace Pengu.Windows.Browser;

/// <summary>
/// Minimal <c>ICoreWebView2EnvironmentOptions</c> implementation. The Diga
/// COM gen exposes the interface via <c>GetX/SetX</c> method pairs (the
/// underlying COM contract is property-pair shaped, not C# properties);
/// this class backs each pair with a private field.
///
/// <para>Custom-scheme registration (for <c>app://hub/</c>) lives on
/// <c>ICoreWebView2EnvironmentOptions4</c> — added when we wire up the
/// asset scheme handler in a follow-up.</para>
/// </summary>
[GeneratedComClass]
internal partial class WebView2EnvironmentOptions : ICoreWebView2EnvironmentOptions
{
    private string _additionalArgs;
    private string _language = string.Empty;
    private string _targetVersion = "90.0.0.0";
    private int _allowSso;

    public WebView2EnvironmentOptions(string additionalArgs)
    {
        _additionalArgs = additionalArgs ?? string.Empty;
    }

    public string GetAdditionalBrowserArguments() => _additionalArgs;
    public void SetAdditionalBrowserArguments(string value) => _additionalArgs = value;

    public string GetLanguage() => _language;
    public void SetLanguage(string value) => _language = value;

    public string GetTargetCompatibleBrowserVersion() => _targetVersion;
    public void SetTargetCompatibleBrowserVersion(string value) => _targetVersion = value;

    public int GetAllowSingleSignOnUsingOSPrimaryAccount() => _allowSso;
    public void SetAllowSingleSignOnUsingOSPrimaryAccount(int value) => _allowSso = value;
}
