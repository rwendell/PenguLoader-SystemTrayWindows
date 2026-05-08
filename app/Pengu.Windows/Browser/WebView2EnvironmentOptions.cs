using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Diga.WebView2.Interop;
using Pengu.Logging;

namespace Pengu.Windows.Browser;

/// <summary>
/// Implements both <see cref="ICoreWebView2EnvironmentOptions"/> and the
/// <see cref="ICoreWebView2EnvironmentOptions4"/> sidecar so we can hand
/// WebView2 a list of custom scheme registrations at env-creation time.
/// Without that step, navigations to <c>app://...</c> silently fall through
/// to <c>about:blank</c> without ever firing <c>WebResourceRequested</c>.
///
/// <para>Diga's COM gen exposes the property pairs as <c>GetX/SetX</c>
/// methods (the underlying COM contract is method-shaped, not C# properties);
/// each pair is backed by a private field.</para>
/// </summary>
[GeneratedComClass]
internal partial class WebView2EnvironmentOptions :
    ICoreWebView2EnvironmentOptions,
    ICoreWebView2EnvironmentOptions4
{
    private static readonly StrategyBasedComWrappers s_wrappers = new();

    private string _additionalArgs;
    private string _language = string.Empty;
    private string _targetVersion = "90.0.0.0";
    private int _allowSso;
    private readonly ICoreWebView2CustomSchemeRegistration[] _schemes;

    public WebView2EnvironmentOptions(string additionalArgs, params ICoreWebView2CustomSchemeRegistration[] schemes)
    {
        _additionalArgs = additionalArgs ?? string.Empty;
        _schemes = schemes ?? Array.Empty<ICoreWebView2CustomSchemeRegistration>();
    }

    // -------- ICoreWebView2EnvironmentOptions --------

    public string GetAdditionalBrowserArguments() => _additionalArgs;
    public void SetAdditionalBrowserArguments(string value) => _additionalArgs = value;

    public string GetLanguage() => _language;
    public void SetLanguage(string value) => _language = value;

    public string GetTargetCompatibleBrowserVersion() => _targetVersion;
    public void SetTargetCompatibleBrowserVersion(string value) => _targetVersion = value;

    public int GetAllowSingleSignOnUsingOSPrimaryAccount() => _allowSso;
    public void SetAllowSingleSignOnUsingOSPrimaryAccount(int value) => _allowSso = value;

    // -------- ICoreWebView2EnvironmentOptions4 --------

    // ICoreWebView2CustomSchemeRegistration interface IID. We QI each managed
    // registration to this interface before handing pointers back to WebView2,
    // because WebView2 invokes the registration via this interface's vtable
    // directly — a bare IUnknown won't do.
    private static readonly Guid s_iidCustomSchemeRegistration = new("D60AC92C-37A6-4B26-A39E-95CFE59047BB");

    public unsafe int GetCustomSchemeRegistrations(out uint count, IntPtr ppRegistrations)
    {
        try
        {
            count = (uint)_schemes.Length;
            if (count == 0)
            {
                if (ppRegistrations != IntPtr.Zero) *(IntPtr*)ppRegistrations = IntPtr.Zero;
                return 0;
            }
            var arr = (IntPtr*)Marshal.AllocCoTaskMem(IntPtr.Size * (int)count);
            for (int i = 0; i < count; i++)
            {
                var unk = s_wrappers.GetOrCreateComInterfaceForObject(_schemes[i], CreateComInterfaceFlags.None);
                int hr = Marshal.QueryInterface(unk, in s_iidCustomSchemeRegistration, out IntPtr iface);
                Marshal.Release(unk);
                if (hr < 0)
                {
                    for (int j = 0; j < i; j++) Marshal.Release(arr[j]);
                    Marshal.FreeCoTaskMem((IntPtr)arr);
                    count = 0;
                    return hr;
                }
                arr[i] = iface;
            }
            *(IntPtr*)ppRegistrations = (IntPtr)arr;
            return 0;
        }
        catch (Exception ex)
        {
            count = 0;
            Log.Error(ex, "GetCustomSchemeRegistrations threw");
            return unchecked((int)0x80004005); // E_FAIL
        }
    }

    public int SetCustomSchemeRegistrations(uint count, ref ICoreWebView2CustomSchemeRegistration schemeRegistrations)
    {
        // Schemes are populated via the ctor; this setter is unused.
        return unchecked((int)0x80004001); // E_NOTIMPL
    }
}

/// <summary>
/// One custom scheme registration handed to WebView2 at env-creation time.
/// For Pengu we register exactly one — <c>app</c> — with secure context +
/// authority component so <c>app://hub/...</c> URLs behave like https
/// origins for service-worker / secure-context APIs.
/// </summary>
[GeneratedComClass]
internal partial class CustomSchemeRegistration : ICoreWebView2CustomSchemeRegistration
{
    private readonly string _schemeName;
    private int _treatAsSecure;
    private int _hasAuthorityComponent;

    public CustomSchemeRegistration(string schemeName, bool treatAsSecure, bool hasAuthorityComponent)
    {
        _schemeName = schemeName;
        _treatAsSecure = treatAsSecure ? 1 : 0;
        _hasAuthorityComponent = hasAuthorityComponent ? 1 : 0;
    }

    public string GetSchemeName() => _schemeName;

    public int GetTreatAsSecure() => _treatAsSecure;
    public void SetTreatAsSecure(int value) => _treatAsSecure = value;

    public int GetHasAuthorityComponent() => _hasAuthorityComponent;
    public void SetHasAuthorityComponent(int value) => _hasAuthorityComponent = value;

    public unsafe void GetAllowedOrigins(out uint count, IntPtr ppOrigins)
    {
        // No origin restrictions; empty list lets every origin (including
        // about: pages) request app:// resources.
        count = 0;
        if (ppOrigins != IntPtr.Zero) *(IntPtr*)ppOrigins = IntPtr.Zero;
    }

    public void SetAllowedOrigins(uint count, ref string allowedOrigins)
    {
        // Unused; allowed-origins are fixed at construction.
    }
}
