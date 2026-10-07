using System.Text.Json;

namespace NetCraft.Game.Server;

//IpBanList IP ban list, maps to vanilla net.minecraft.server.players.IpBanList
//Persisted to banned-ips.json with fields aligned with vanilla ip/created/source/expires/reason
public sealed class IpBanList : StoredJsonList<IpBanEntry>
{
    //DefaultReason default text when no reason is given, aligned with vanilla BAN_REASON
    public const string DefaultReason = "Banned by an operator.";

    public IpBanList(string path) : base(path) { }

    //IsBanned whether the IP is banned; a test connection without an IP is passed through
    public bool IsBanned(string? address) => address is not null && Find(address) is not null;

    //Find finds a record by IP text, case-insensitive; IPv6 text is case-insensitive
    public IpBanEntry? Find(string address)
    {
        foreach (var entry in Entries)
            if (string.Equals(entry.Ip, address, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }

    //Add writes an IP ban and persists it; returns false when already on the list
    public bool Add(string address, string source, string reason)
    {
        if (Find(address) is not null) return false;
        AddEntry(new IpBanEntry(address, DateTimeOffset.UtcNow.ToString("o"),
            source, BanList.Forever, reason));
        return true;
    }

    //Remove unbans the IP and returns whether a record was hit
    public bool Remove(string address)
    {
        var existing = Find(address);
        return existing is not null && RemoveEntry(entry => ReferenceEquals(entry, existing));
    }

    //ReadEntry reads one IP ban record; a missing ip makes the entry invalid and it is skipped
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

//IpBanEntry a single IP ban record, fields aligned with vanilla IpBanListEntry
public sealed record IpBanEntry(string Ip, string Created, string Source, string Expires, string Reason);
