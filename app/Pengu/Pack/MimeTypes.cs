namespace Pengu.Pack;

/// <summary>
/// Tiny MIME-type lookup keyed off filename extension. Covers the file types
/// the SolidJS hub bundle actually contains; anything we don't recognise
/// gets <c>application/octet-stream</c> so WebView2 doesn't treat it as
/// HTML by accident.
///
/// <para>Extensions are matched case-insensitively. The leading dot is
/// expected (<c>.html</c>, not <c>html</c>) — same shape as
/// <see cref="System.IO.Path.GetExtension(string)"/>.</para>
/// </summary>
public static class MimeTypes
{
    public static string ForExtension(string? ext) => (ext ?? string.Empty).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" or ".mjs"   => "application/javascript; charset=utf-8",
        ".css"            => "text/css; charset=utf-8",
        ".json"           => "application/json; charset=utf-8",
        ".svg"            => "image/svg+xml",
        ".png"            => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".ico"            => "image/x-icon",
        ".woff"           => "font/woff",
        ".woff2"          => "font/woff2",
        ".ttf"            => "font/ttf",
        ".otf"            => "font/otf",
        ".map"            => "application/json",
        ".wasm"           => "application/wasm",
        ".txt"            => "text/plain; charset=utf-8",
        _                 => "application/octet-stream",
    };
}
