using System.Text.Json;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;

namespace NetCraft.Game.Server;

//OpList operator list, maps to vanilla net.minecraft.server.players.ServerOpList
//Persisted to ops.json with fields aligned with vanilla uuid/name/level/bypassesPlayerLimit
//Name matching as a fallback: in offline mode the uuid is derived from the name, so a list with only the name still hits
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

    //Path the list file path
    public string Path => _path;

    //Count the number of list entries
    public int Count => _byName.Count;

    //Names a snapshot of list names, for diagnostics
    public IReadOnlyList<string> Names => _byName.Values.Select(e => e.Name).ToList();

    //GetPermissionLevel gets a player's permission level; 0 when not on the list, maps to vanilla getProfilePermissions
    public int GetPermissionLevel(GameProfile profile) => Find(profile)?.Level ?? 0;

    //IsOp whether it is on the list
    public bool IsOp(GameProfile profile) => Find(profile) is not null;

    //Add writes or updates the list and persists it; returns whether an entry was added
    public bool Add(GameProfile profile, int level)
    {
        var added = Find(profile) is null;
        var entry = new OpEntry(profile.Id, profile.Name, Math.Clamp(level, 0, 4), false);
        Index(entry);
        Save();
        return added;
    }

    //Remove removes from the list and persists it; returns whether an entry was hit
    public bool Remove(GameProfile profile)
    {
        var existing = Find(profile);
        if (existing is null) return false;
        _byId.Remove(existing.Id);
        _byName.Remove(existing.Name);
        Save();
        return true;
    }

    //Find looks up by uuid then by name; the two indexes may point to the same player
    private OpEntry? Find(GameProfile profile)
    {
        if (_byId.TryGetValue(profile.Id, out var byId)) return byId;
        return _byName.TryGetValue(profile.Name, out var byName) ? byName : null;
    }

    //Index registers an entry; an old same-name entry is cleared first so two uuids do not fight over one name
    private void Index(OpEntry entry)
    {
        if (_byName.TryGetValue(entry.Name, out var previous))
            _byId.Remove(previous.Id);
        _byId.Remove(entry.Id);
        _byName[entry.Name] = entry;
    }

    //Load reads the list; a missing file creates an empty list, and a parse failure is treated as an empty list without blocking startup
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
                //When the uuid is missing or invalid only the name index is used; a hand-written list may contain only names
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

    //Save writes the list; a write failure only logs and does not affect running
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

    //OpEntry a single operator record
    private sealed record OpEntry(Guid Id, string Name, int Level, bool BypassesPlayerLimit);
}
