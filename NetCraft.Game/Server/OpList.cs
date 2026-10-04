using System.Text.Json;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;

namespace NetCraft.Game.Server;

//OpList 管理员名单对应原版 net.minecraft.server.players.ServerOpList
//落盘 ops.json 字段对齐原版 uuid/name/level/bypassesPlayerLimit
//名字匹配作为回退: 离线模式 uuid 随名字生成 名单只写名字时也能命中
public sealed class OpList
{
    private readonly string _path;
    private readonly Dictionary<Guid, OpEntry> _byId = new();
    private readonly Dictionary<string, OpEntry> _byName = new(StringComparer.OrdinalIgnoreCase);

    public OpList(string path)
    {
        _path = path;
        Load();
    }

    //Path 名单文件路径
    public string Path => _path;

    //Count 名单条目数
    public int Count => _byName.Count;

    //Names 名单名字快照供诊断
    public IReadOnlyList<string> Names => _byName.Values.Select(e => e.Name).ToList();

    //GetPermissionLevel 取玩家权限等级 不在名单返回 0 对应原版 getProfilePermissions
    public int GetPermissionLevel(GameProfile profile) => Find(profile)?.Level ?? 0;

    //IsOp 是否在名单内
    public bool IsOp(GameProfile profile) => Find(profile) is not null;

    //Add 写入或更新名单并落盘 返回是否新增条目
    public bool Add(GameProfile profile, int level)
    {
        var added = Find(profile) is null;
        var entry = new OpEntry(profile.Id, profile.Name, Math.Clamp(level, 0, 4), false);
        Index(entry);
        Save();
        return added;
    }

    //Remove 从名单移除并落盘 返回是否命中条目
    public bool Remove(GameProfile profile)
    {
        var existing = Find(profile);
        if (existing is null) return false;
        _byId.Remove(existing.Id);
        _byName.Remove(existing.Name);
        Save();
        return true;
    }

    //Find 先按 uuid 再按名字查 两个索引可能指向同一玩家
    private OpEntry? Find(GameProfile profile)
    {
        if (_byId.TryGetValue(profile.Id, out var byId)) return byId;
        return _byName.TryGetValue(profile.Name, out var byName) ? byName : null;
    }

    //Index 登记条目 同名旧条目先清掉避免两个 uuid 抢一个名字
    private void Index(OpEntry entry)
    {
        if (_byName.TryGetValue(entry.Name, out var previous))
            _byId.Remove(previous.Id);
        _byId.Remove(entry.Id);
        _byName[entry.Name] = entry;
    }

    //Load 读名单 文件缺失生成空名单 解析失败按空名单处理不阻断启动
    private void Load()
    {
        _byId.Clear();
        _byName.Clear();
        if (!File.Exists(_path))
        {
            Log.Info($"ops.json does not exist, generating an empty list at {_path}");
            Save();
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                Log.Warning($"ops.json top level is not an array, treating it as empty {_path}");
                return;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                var name = element.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    Log.Warning($"ops.json entry is missing name, skipped {element}");
                    continue;
                }
                var level = element.TryGetProperty("level", out var levelNode) && levelNode.TryGetInt32(out var parsed)
                    ? Math.Clamp(parsed, 0, 4)
                    : 4;
                var bypasses = element.TryGetProperty("bypassesPlayerLimit", out var bypassNode)
                    && bypassNode.ValueKind == JsonValueKind.True;
                var idText = element.TryGetProperty("uuid", out var idNode) ? idNode.GetString() : null;
                //uuid 缺失或非法时只用名字索引 手写名单可以只填名字
                var id = Guid.TryParse(idText, out var parsedId) ? parsedId : Guid.Empty;
                var entry = new OpEntry(id, name, level, bypasses);
                _byName[name] = entry;
                if (id != Guid.Empty) _byId[id] = entry;
            }
            Log.Info($"Operator list loaded {_path} with {_byName.Count} entries");
        }
        catch (Exception e)
        {
            Log.Error($"ops.json parse failed, treating it as empty {_path}: {e.Message}");
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
                foreach (var entry in _byName.Values)
                {
                    writer.WriteStartObject();
                    writer.WriteString("uuid", entry.Id.ToString("D"));
                    writer.WriteString("name", entry.Name);
                    writer.WriteNumber("level", entry.Level);
                    writer.WriteBoolean("bypassesPlayerLimit", entry.BypassesPlayerLimit);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            File.WriteAllText(_path, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (Exception e)
        {
            Log.Error($"ops.json write failed {_path}: {e.Message}");
        }
    }

    //OpEntry 单条管理员记录
    private sealed record OpEntry(Guid Id, string Name, int Level, bool BypassesPlayerLimit);
}
