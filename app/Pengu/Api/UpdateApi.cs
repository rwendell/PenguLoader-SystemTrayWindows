using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pengu.Bridge;
using Pengu.Logging;

namespace Pengu.Api;

/// <summary>
/// Notification-only update checker exposed as <c>window.pengu.update</c>.
/// One bridge method: <see cref="Check"/> hits GitHub Releases for the
/// upstream <c>latest</c>, compares <c>tag_name</c> against
/// <see cref="AppEnv.AppVersion"/>, and returns an <see cref="UpdateInfo"/>
/// when newer. Hub renders a "view release" affordance; we do <b>not</b>
/// download or apply anything in-process. See <c>docs/app-hub.md</c> §13
/// for the rationale (cross-platform auto-apply + zip integrity + locked
/// <c>core.dll</c> + Gatekeeper combine into more failure surface than the
/// once-a-year release cadence pays back).
///
/// <para>Returns <c>null</c> when the running build is up-to-date or the
/// remote tag isn't a parseable SemVer. Network / HTTP failures throw —
/// the bridge dispatcher surfaces them as a Promise rejection so the hub
/// can show "Failed: &lt;reason&gt;" rather than a confused "no update".</para>
/// </summary>
[JsInterop("update")]
public partial class UpdateApi
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/PenguLoader/PenguLoader/releases/latest";

    [JsInvokable]
    public async Task<UpdateInfo?> Check()
    {
        var release = await FetchLatestAsync().ConfigureAwait(false);
        if (release is null) return null;

        if (!TryParseRemoteVersion(release.TagName, out var remote))
        {
            Log.Debug("UpdateApi: unparseable remote tag '{0}'; treating as no-update", release.TagName);
            return null;
        }
        if (!Version.TryParse(AppEnv.AppVersion, out var local))
        {
            Log.Warn("UpdateApi: local version '{0}' isn't parseable", AppEnv.AppVersion);
            return null;
        }
        if (remote.CompareTo(local) <= 0)
        {
            Log.Debug("UpdateApi: up-to-date (local={0}, remote={1})", local, remote);
            return null;
        }

        var url = !string.IsNullOrEmpty(release.HtmlUrl)
            ? release.HtmlUrl
            : $"https://github.com/PenguLoader/PenguLoader/releases/tag/{release.TagName}";

        return new UpdateInfo(release.TagName, release.Body ?? string.Empty, url);
    }

    private static async Task<GithubRelease?> FetchLatestAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API requires a User-Agent and recommends the
        // application/vnd.github+json Accept header.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Pengu/{AppEnv.AppVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var resp = await http.GetAsync(LatestReleaseUrl).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var body = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer
            .DeserializeAsync(body, PenguJsonContext.Default.GithubRelease)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Parse a release tag like <c>v1.2.3</c> / <c>1.2.3</c> /
    /// <c>v1.2.3-rc1</c> into a <see cref="Version"/>. Strips the leading
    /// <c>v</c>/<c>V</c> and anything after a <c>-</c> (prerelease /
    /// build metadata) so <see cref="Version.TryParse"/> succeeds.
    /// </summary>
    internal static bool TryParseRemoteVersion(string? tag, out Version version)
    {
        version = new Version();
        if (string.IsNullOrEmpty(tag)) return false;
        var s = tag.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];
        var dash = s.IndexOf('-');
        if (dash > 0) s = s[..dash];
        return Version.TryParse(s, out version!);
    }
}

/// <summary>Result of <c>pengu.update.check()</c>. Null when up-to-date.</summary>
public sealed record UpdateInfo(string Tag, string Body, string Url);

/// <summary>Subset of the GitHub Releases API JSON we deserialize. Property
/// names mirror the wire shape; we only pull the three fields the hub
/// surfaces so the source-genned JSON contract stays small.
///
/// <para>Public for AOT plumbing only: <see cref="PenguJsonContext"/> is
/// public, and the source generator's emitted <c>JsonTypeInfo&lt;T&gt;</c>
/// property has to match its host's accessibility. Not consumed outside
/// the assembly.</para></summary>
public sealed record GithubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("body")]     string? Body,
    [property: JsonPropertyName("html_url")] string? HtmlUrl);
