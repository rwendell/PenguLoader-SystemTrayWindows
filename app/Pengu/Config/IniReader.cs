namespace Pengu.Config;

/// <summary>
/// Parser for the <c>config</c> file format: <c>key = value</c> pairs grouped
/// under <c>[section]</c> headers, with <c>;</c> or <c>#</c> comment lines.
/// Whitespace around keys, values, and section names is trimmed. Lines that
/// don't match a section header or a key/value pair are silently dropped.
/// </summary>
public static class IniReader
{
    public static IniMap Parse(string content)
    {
        var map = new IniMap();
        IniMap.Section? current = null;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;
            if (line[0] == ';' || line[0] == '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                var name = line[1..^1].Trim();
                if (name.Length > 0) current = map.GetOrCreate(name);
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0) continue; // not a key/value pair

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0) continue;

            // Pre-section keys go into a synthetic empty-named section. Riot's
            // config never has these, but defending against it costs nothing.
            current ??= map.GetOrCreate(string.Empty);
            current.Set(key, value);
        }

        return map;
    }

    /// <summary>
    /// Try to parse a value as a boolean. Accepts <c>1</c>/<c>0</c>,
    /// <c>true</c>/<c>false</c>, <c>yes</c>/<c>no</c> (case-insensitive,
    /// trimmed). Returns <paramref name="defaultValue"/> on miss.
    /// </summary>
    public static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        var v = value.Trim();
        if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase) || v.Equals("no", StringComparison.OrdinalIgnoreCase))
            return false;
        return defaultValue;
    }

    public static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return int.TryParse(value.Trim(), out var n) ? n : defaultValue;
    }
}
