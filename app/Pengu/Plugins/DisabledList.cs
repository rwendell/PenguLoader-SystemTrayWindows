namespace Pengu.Plugins;

/// <summary>
/// Parse / format the comma-separated lowercase-hex FNV-1a hashes stored in
/// the config's <c>app.disabled_plugins</c> key. Convention matches the
/// existing TS implementation and the C++ core's preload loader so all three
/// components agree.
///
/// <para>Both directions are tolerant: parse skips empty / non-hex entries;
/// format emits sorted lowercase 8-digit hex. Sorting + zero-padding makes
/// the on-disk value diff-stable so config-file diffs reflect intent, not
/// hash-set iteration order.</para>
/// </summary>
public static class DisabledList
{
    public static HashSet<uint> Parse(string? csv)
    {
        var set = new HashSet<uint>();
        if (string.IsNullOrWhiteSpace(csv)) return set;

        foreach (var raw in csv.Split(','))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;
            if (uint.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture, out var n))
            {
                set.Add(n);
            }
        }
        return set;
    }

    public static string Format(IEnumerable<uint> hashes)
    {
        var sorted = hashes.Distinct().OrderBy(h => h).ToArray();
        return string.Join(",", sorted.Select(h => h.ToString("x8")));
    }
}
