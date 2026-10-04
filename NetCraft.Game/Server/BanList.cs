using System.Text.Json;
using NetCraft.Network.Protocol.Login;

namespace NetCraft.Game.Server;

//BanList 玩家封禁名单对应原版 net.minecraft.server.players.UserBanList
//落盘 banned-players.json 字段对齐原版 uuid/name/created/source/expires/reason
//名字匹配作为回退: 离线模式 uuid 随名字生成 手写名单只填名字也能命中
public sealed class BanList : StoredJsonList<BanEntry>
{
    //Forever 永不过期 原版本作不做过期时间 封禁一律写 forever
    public const string Forever = "forever";

    //DefaultReason 未指定理由时的默认文案 对齐原版 BAN_REASON
    public const string DefaultReason = "Banned by an operator.";

    public BanList(string path) : base(path) { }

    //IsBanned 玩家是否被封禁
    public bool IsBanned(GameProfile profile) => Find(profile) is not null;

    //Find 先按 uuid 再按名字查 与 OpList 同一套双索引回退策略
    public BanEntry? Find(GameProfile profile)
    {
        foreach (var entry in Entries)
            if (entry.Id != Guid.Empty && entry.Id == profile.Id) return entry;
        foreach (var entry in Entries)
            if (string.Equals(entry.Name, profile.Name, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }

    //Add 写入封禁记录并落盘 已在名单内返回 false
    public bool Add(GameProfile profile, string source, string reason)
    {
        if (Find(profile) is not null) return false;
        AddEntry(new BanEntry(profile.Id, profile.Name, DateTimeOffset.UtcNow.ToString("o"),
            source, Forever, reason));
        return true;
    }

    //Remove 解除封禁 返回是否命中记录
    public bool Remove(GameProfile profile)
    {
        var existing = Find(profile);
        return existing is not null && RemoveEntry(entry => ReferenceEquals(entry, existing));
    }

    //ReadEntry 读一条封禁记录 name 缺失视为无效条目跳过
    protected override BanEntry? ReadEntry(JsonElement element)
    {
        var name = element.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) return null;
        var idText = element.TryGetProperty("uuid", out var idNode) ? idNode.GetString() : null;
        return new BanEntry(
            Guid.TryParse(idText, out var id) ? id : Guid.Empty,
            name,
            ReadString(element, "created"),
            ReadString(element, "source"),
            ReadString(element, "expires", Forever),
            ReadString(element, "reason", DefaultReason));
    }

    protected override void WriteEntry(Utf8JsonWriter writer, BanEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("uuid", entry.Id.ToString("D"));
        writer.WriteString("name", entry.Name);
        writer.WriteString("created", entry.Created);
        writer.WriteString("source", entry.Source);
        writer.WriteString("expires", entry.Expires);
        writer.WriteString("reason", entry.Reason);
        writer.WriteEndObject();
    }

    private static string ReadString(JsonElement element, string key, string fallback = "")
        => element.TryGetProperty(key, out var node) && node.GetString() is { } text ? text : fallback;
}

//BanEntry 单条玩家封禁记录 字段对齐原版 UserBanListEntry
public sealed record BanEntry(Guid Id, string Name, string Created, string Source, string Expires, string Reason);
