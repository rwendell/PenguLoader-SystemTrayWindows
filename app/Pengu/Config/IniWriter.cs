using System.Text;

namespace Pengu.Config;

/// <summary>
/// Emits <see cref="IniMap"/> back to text. Sections are separated by a blank
/// line; entries inside a section are <c>key = value</c> on one line each.
/// Order of sections and keys matches the <see cref="IniMap"/> insertion order
/// so a round-trip read+write only reorders content if a caller explicitly
/// sets a new key.
/// </summary>
public static class IniWriter
{
    public static string Write(IniMap map)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var section in map.Sections)
        {
            if (!first) sb.Append('\n');
            first = false;

            if (!string.IsNullOrEmpty(section.Name))
            {
                sb.Append('[').Append(section.Name).Append(']').Append('\n');
            }

            foreach (var (key, value) in section.Entries)
            {
                sb.Append(key).Append(" = ").Append(value).Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>Format a boolean for the ini file. Uses lowercase
    /// <c>true</c>/<c>false</c> to match v1.1.6's output and the existing
    /// values found in <c>bin/config</c>.</summary>
    public static string FormatBool(bool value) => value ? "true" : "false";
}
