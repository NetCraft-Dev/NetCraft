using System.Text.Json;
using NetCraft.Network.Protocol.Login;

namespace NetCraft.Game.Server;

//BanList player ban list, maps to vanilla net.minecraft.server.players.UserBanList
//Persisted to banned-players.json with fields aligned with vanilla uuid/name/created/source/expires/reason
//Name matching as a fallback: in offline mode the uuid is derived from the name, so a hand-written list with only the name still hits
public sealed class BanList : StoredJsonList<BanEntry>
{
    //Forever never expires; like vanilla, this project does no expiry and always writes forever
    public const string Forever = "forever";

    //DefaultReason default text when no reason is given, aligned with vanilla BAN_REASON
    public const string DefaultReason = "Banned by an operator.";

    public BanList(string path) : base(path) { }

    //IsBanned whether the player is banned
    public bool IsBanned(GameProfile profile) => Find(profile) is not null;

    //Find looks up by uuid then by name, the same dual-index fallback strategy as OpList
    public BanEntry? Find(GameProfile profile)
    {
        foreach (var entry in Entries)
            if (entry.Id != Guid.Empty && entry.Id == profile.Id) return entry;
        foreach (var entry in Entries)
            if (string.Equals(entry.Name, profile.Name, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }

    //Add writes a ban record and persists it; returns false when already on the list
    public bool Add(GameProfile profile, string source, string reason)
    {
        if (Find(profile) is not null) return false;
        AddEntry(new BanEntry(profile.Id, profile.Name, DateTimeOffset.UtcNow.ToString("o"),
            source, Forever, reason));
        return true;
    }

    //Remove unbans and returns whether a record was hit
    public bool Remove(GameProfile profile)
    {
        var existing = Find(profile);
        return existing is not null && RemoveEntry(entry => ReferenceEquals(entry, existing));
    }

    //ReadEntry reads one ban record; a missing name makes the entry invalid and it is skipped
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

//BanEntry a single player ban record, fields aligned with vanilla UserBanListEntry
public sealed record BanEntry(Guid Id, string Name, string Created, string Source, string Expires, string Reason);
