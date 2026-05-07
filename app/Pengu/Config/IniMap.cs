namespace Pengu.Config;

/// <summary>
/// Lossless in-memory ini representation: ordered list of sections, each with
/// an ordered list of <c>(key, value)</c> pairs. Keys preserve their original
/// case + ordering; comments / blank lines are dropped (we don't round-trip
/// those).
///
/// <para>The "lossless" part matters: the C++ core reads keys we don't
/// model in <see cref="ConfigSnapshot"/> (e.g. the undocumented
/// <c>debug_port</c>). Round-tripping unknown keys keeps them intact.</para>
/// </summary>
public sealed class IniMap
{
    private readonly List<Section> _sections = new();

    public IReadOnlyList<Section> Sections => _sections;

    public Section GetOrCreate(string name)
    {
        var existing = _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var s = new Section(name);
        _sections.Add(s);
        return s;
    }

    public string? Get(string section, string key)
    {
        var s = _sections.FirstOrDefault(x => string.Equals(x.Name, section, StringComparison.OrdinalIgnoreCase));
        return s?.Get(key);
    }

    public void Set(string section, string key, string value)
        => GetOrCreate(section).Set(key, value);

    public sealed class Section
    {
        public string Name { get; }
        private readonly List<KeyValuePair<string, string>> _entries = new();

        public Section(string name) { Name = name; }

        public IReadOnlyList<KeyValuePair<string, string>> Entries => _entries;

        public string? Get(string key)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return _entries[i].Value;
            return null;
        }

        public void Set(string key, string value)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _entries[i] = new KeyValuePair<string, string>(_entries[i].Key, value);
                    return;
                }
            }
            _entries.Add(new KeyValuePair<string, string>(key, value));
        }
    }
}
