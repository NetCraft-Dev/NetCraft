using System.Globalization;
using System.Text;

namespace NetCraft;

//PropertiesConfig 通用 properties 文件读写容器
//对应原版 com.mojang.util.PropertiesUtils 简化版
//支持 # 注释 key=value 行 加载与保存到指定路径
public sealed class PropertiesConfig
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly List<string> _comments = new();

    //索引器读写 key 对应 value 不存在返回空字符串
    public string this[string key]
    {
        get => _values.GetValueOrDefault(key, string.Empty);
        set => _values[key] = value;
    }

    //GetOrDefault 查询 key 不存在返回默认值
    public string GetOrDefault(string key, string defaultValue)
        => _values.TryGetValue(key, out var v) ? v : defaultValue;

    //GetInt 解析 int 不存在或失败返回 defaultValue
    public int GetInt(string key, int defaultValue)
        => int.TryParse(GetOrDefault(key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    //GetBool 解析 bool 不存在或失败返回 defaultValue
    public bool GetBool(string key, bool defaultValue)
        => bool.TryParse(GetOrDefault(key, string.Empty), out var v) ? v : defaultValue;

    //GetFloat 解析 float 不存在或失败返回 defaultValue
    public float GetFloat(string key, float defaultValue)
        => float.TryParse(GetOrDefault(key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    //GetIntBytes 解析字节数表示的字符串如 256MB 返回字节数
    //后缀支持 K/M/G 不区分大小写 简化版仅 K/M/G 三档
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

    //Set 写入 key=value
    public void Set(string key, string value) => _values[key] = value;

    //SetInt 写入 int 字段
    public void SetInt(string key, int value) => _values[key] = value.ToString(CultureInfo.InvariantCulture);

    //SetBool 写入 bool 字段
    public void SetBool(string key, bool value) => _values[key] = value ? "true" : "false";

    //ContainsKey 是否含指定 key
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    //Load 从指定路径读取 properties 文件
    //文件不存在返回空容器不抛
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

    //Save 保存到指定路径 覆盖已有文件
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

    //EscapeValue 简单转义 value 中的换行和等号
    private static string EscapeValue(string value)
        => value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");

    private static string UnescapeValue(string value)
        => value.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\\\", "\\");
}
