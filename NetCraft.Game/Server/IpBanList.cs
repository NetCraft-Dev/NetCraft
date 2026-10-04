using System.Text.Json;

namespace NetCraft.Game.Server;

//IpBanList IP 封禁名单对应原版 net.minecraft.server.players.IpBanList
//落盘 banned-ips.json 字段对齐原版 ip/created/source/expires/reason
public sealed class IpBanList : StoredJsonList<IpBanEntry>
{
    //DefaultReason 未指定理由时的默认文案 对齐原版 BAN_REASON
    public const string DefaultReason = "Banned by an operator.";

    public IpBanList(string path) : base(path) { }

    //IsBanned 该 IP 是否被封禁 无 IP 的测试连接直接放行
    public bool IsBanned(string? address) => address is not null && Find(address) is not null;

    //Find 按 IP 文本查记录 不区分大小写 IPv6 文本大小写不敏感
    public IpBanEntry? Find(string address)
    {
        foreach (var entry in Entries)
            if (string.Equals(entry.Ip, address, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }

    //Add 写入 IP 封禁并落盘 已在名单内返回 false
    public bool Add(string address, string source, string reason)
    {
        if (Find(address) is not null) return false;
        AddEntry(new IpBanEntry(address, DateTimeOffset.UtcNow.ToString("o"),
            source, BanList.Forever, reason));
        return true;
    }

    //Remove 解除 IP 封禁 返回是否命中记录
    public bool Remove(string address)
    {
        var existing = Find(address);
        return existing is not null && RemoveEntry(entry => ReferenceEquals(entry, existing));
    }

    //ReadEntry 读一条 IP 封禁记录 ip 缺失视为无效条目跳过
    protected override IpBanEntry? ReadEntry(JsonElement element)
    {
        var ip = element.TryGetProperty("ip", out var ipNode) ? ipNode.GetString() : null;
        if (string.IsNullOrWhiteSpace(ip)) return null;
        return new IpBanEntry(
            ip,
            ReadString(element, "created"),
            ReadString(element, "source"),
            ReadString(element, "expires", BanList.Forever),
            ReadString(element, "reason", DefaultReason));
    }

    protected override void WriteEntry(Utf8JsonWriter writer, IpBanEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("ip", entry.Ip);
        writer.WriteString("created", entry.Created);
        writer.WriteString("source", entry.Source);
        writer.WriteString("expires", entry.Expires);
        writer.WriteString("reason", entry.Reason);
        writer.WriteEndObject();
    }

    private static string ReadString(JsonElement element, string key, string fallback = "")
        => element.TryGetProperty(key, out var node) && node.GetString() is { } text ? text : fallback;
}

//IpBanEntry 单条 IP 封禁记录 字段对齐原版 IpBanListEntry
public sealed record IpBanEntry(string Ip, string Created, string Source, string Expires, string Reason);
