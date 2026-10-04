using System.Text.Json;
using NetCraft.Logging;

namespace NetCraft.Game.Server;

//StoredJsonList 名单文件读写基类 对应原版 net.minecraft.server.players.StoredUserList
//负责 JSON 数组的加载与落盘 单条记录的字段读写由子类实现
//文件缺失生成空名单 解析失败按空名单处理不阻断启动 与 OpList 同策略
public abstract class StoredJsonList<TEntry> where TEntry : class
{
    private readonly string _path;
    private readonly List<TEntry> _entries = new();

    protected StoredJsonList(string path)
    {
        _path = path;
        Load();
    }

    //Path 名单文件路径
    public string Path => _path;

    //Count 名单条目数
    public int Count => _entries.Count;

    //Entries 条目只读视图 供 banlist 命令列出
    public IReadOnlyList<TEntry> Entries => _entries;

    //AddEntry 追加一条记录并落盘
    protected void AddEntry(TEntry entry)
    {
        _entries.Add(entry);
        Save();
    }

    //RemoveEntry 移除首条命中记录并落盘 返回是否命中
    protected bool RemoveEntry(Predicate<TEntry> match)
    {
        var index = _entries.FindIndex(match);
        if (index < 0) return false;
        _entries.RemoveAt(index);
        Save();
        return true;
    }

    //ReadEntry 从 JSON 对象读出一条记录 字段缺失返回 null 表示跳过该条
    protected abstract TEntry? ReadEntry(JsonElement element);

    //WriteEntry 把一条记录写成 JSON 对象
    protected abstract void WriteEntry(Utf8JsonWriter writer, TEntry entry);

    //FileName 名单文件名 日志用
    private string FileName => System.IO.Path.GetFileName(_path);

    //Load 读名单 文件缺失生成空名单 解析失败按空名单处理
    private void Load()
    {
        _entries.Clear();
        if (!File.Exists(_path))
        {
            Log.Info($"{FileName} does not exist, generating an empty list at {_path}");
            Save();
            return;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                Log.Warning($"{FileName} top level is not an array, treating it as empty {_path}");
                return;
            }
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var entry = ReadEntry(element);
                if (entry is not null) _entries.Add(entry);
            }
            Log.Info($"List loaded {_path} with {_entries.Count} entries");
        }
        catch (Exception e)
        {
            Log.Error($"{FileName} parse failed, treating it as empty {_path}: {e.Message}");
        }
    }

    //Save 写名单 写盘失败只记日志不影响运行
    private void Save()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartArray();
                foreach (var entry in _entries) WriteEntry(writer, entry);
                writer.WriteEndArray();
            }
            File.WriteAllText(_path, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (Exception e)
        {
            Log.Error($"List write failed {_path}: {e.Message}");
        }
    }
}
