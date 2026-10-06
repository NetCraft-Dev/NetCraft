using System.Globalization;
using System.Text;

namespace NetCraft;

//PropertiesConfig generic properties file read/write container
//Simplified version of vanilla com.mojang.util.PropertiesUtils
//Supports # comments and key=value lines; loads from and saves to the given path
public sealed class PropertiesConfig
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly List<string> _comments = new();

    //Indexer reads/writes the value for a key; returns an empty string when absent
    public string this[string key]
    {
        get => _values.GetValueOrDefault(key, string.Empty);
        set => _values[key] = value;
    }

    //GetOrDefault looks up a key; returns the default when absent
    public string GetOrDefault(string key, string defaultValue)
        => _values.TryGetValue(key, out var v) ? v : defaultValue;

    //GetInt parses an int; returns defaultValue when absent or failed
    public int GetInt(string key, int defaultValue)
        => int.TryParse(GetOrDefault(key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    //GetBool parses a bool; returns defaultValue when absent or failed
    public bool GetBool(string key, bool defaultValue)
        => bool.TryParse(GetOrDefault(key, string.Empty), out var v) ? v : defaultValue;

    //GetFloat parses a float; returns defaultValue when absent or failed
    public float GetFloat(string key, float defaultValue)
        => float.TryParse(GetOrDefault(key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    //GetIntBytes parses a byte-count string like 256MB and returns the number of bytes
    //Suffixes K/M/G are case-insensitive; this simplified version only supports K/M/G
    public long GetSize(string key, long defaultValue)
    {
        var raw = GetOrDefault(key, string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        var span = raw.AsSpan().Trim();
        long multiplier = 1;
        if (span.Length > 0)
        {
            char last = char.ToUpperInvariant(span[^1]);
            if (last == 'K') { multiplier = 1024L; span = span[..^1]; }
            else if (last == 'M') { multiplier = 1024L * 1024; span = span[..^1]; }
            else if (last == 'G') { multiplier = 1024L * 1024 * 1024; span = span[..^1]; }
        }
        return long.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v * multiplier : defaultValue;
    }

    //Set writes key=value
    public void Set(string key, string value) => _values[key] = value;

    //SetInt writes an int field
    public void SetInt(string key, int value) => _values[key] = value.ToString(CultureInfo.InvariantCulture);

    //SetBool writes a bool field
    public void SetBool(string key, bool value) => _values[key] = value ? "true" : "false";

    //ContainsKey checks whether the given key exists
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    //Load reads a properties file from the given path
    //Returns an empty container when the file does not exist; does not throw
    public void Load(string path)
    {
        _values.Clear();
        _comments.Clear();
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty) continue;
            if (trimmed[0] == '#' || trimmed[0] == '!')
            {
                _comments.Add(line);
                continue;
            }
            int eq = trimmed.IndexOf('=');
            if (eq < 0) eq = trimmed.IndexOf(':');
            if (eq <= 0) continue;
            var key = trimmed[..eq].Trim().ToString();
            var value = trimmed[(eq + 1)..].Trim().ToString();
            _values[key] = UnescapeValue(value);
        }
    }

    //Save writes to the given path, overwriting the existing file
    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        foreach (var c in _comments)
        {
            sb.Append(c).Append('\n');
        }
        foreach (var kv in _values)
        {
            sb.Append(kv.Key).Append('=').Append(EscapeValue(kv.Value)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }

    //EscapeValue simply escapes newlines and equals signs in the value
    private static string EscapeValue(string value)
        => value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");

    private static string UnescapeValue(string value)
        => value.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\\\", "\\");
}
