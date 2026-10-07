using System.Text.Json;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;

namespace NetCraft.Game.Server;

//WhiteList whitelist, maps to vanilla net.minecraft.server.players.WhiteList
//Persisted to whitelist.json with fields aligned with vanilla uuid/name; a list with only a name still hits by name
public sealed class WhiteList
{
    private readonly string _path;
    private readonly Dictionary<Guid, WhiteListEntry> _byId = new();
    private readonly Dictionary<string, WhiteListEntry> _byName = new(StringComparer.OrdinalIgnoreCase);

    public WhiteList(string path)
    {
        _path = path;
        Load();
    }

    //Path the list file path
    public string Path => _path;

    //Count the number of list entries
    public int Count => _byName.Count;

    //Names a snapshot of list names, listed by the command
    public IReadOnlyList<string> Names => _byName.Values.Select(e => e.Name).ToList();

    //IsAllowed whether it is on the whitelist
    public bool IsAllowed(GameProfile profile) => Find(profile) is not null;

    //Add writes to the list and persists it; returns whether an entry was added
    public bool Add(GameProfile profile)
    {
        var added = Find(profile) is null;
        Index(new WhiteListEntry(profile.Id, profile.Name));
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

    //Find looks up by uuid then by name
    private WhiteListEntry? Find(GameProfile profile)
    {
        if (_byId.TryGetValue(profile.Id, out var byId)) return byId;
        return _byName.TryGetValue(profile.Name, out var byName) ? byName : null;
    }

    //Index registers an entry; an old same-name entry is cleared first so two uuids do not fight over one name
    private void Index(WhiteListEntry entry)
    {
        if (_byName.TryGetValue(entry.Name, out var previous))
            _byId.Remove(previous.Id);
        _byId.Remove(entry.Id);
        _byName[entry.Name] = entry;
    }

    //Reload re-reads the list from disk, used by the whitelist reload command
    public void Reload() => Load();

    //Load reads the list; a missing file creates an empty list and a parse failure is treated as empty without blocking startup
    private void Load()
    {
        _byId.Clear();
        _byName.Clear();
        if (!File.Exists(_path))
        {
            Log.Info($"whitelist.json does not exist, generating an empty list at {_path}");
            Save();
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                Log.Warning($"whitelist.json top level is not an array, treating it as empty {_path}");
                return;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                var name = element.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    Log.Warning($"whitelist.json entry is missing name, skipped {element}");
                    continue;
                }
                var idText = element.TryGetProperty("uuid", out var idNode) ? idNode.GetString() : null;
                //When the uuid is missing or invalid only the name index is used; a hand-written list may contain only names
                var id = Guid.TryParse(idText, out var parsedId) ? parsedId : Guid.Empty;
                var entry = new WhiteListEntry(id, name);
                _byName[name] = entry;
                if (id != Guid.Empty) _byId[id] = entry;
            }
            Log.Info($"Whitelist loaded {_path} with {_byName.Count} entries");
        }
        catch (Exception e)
        {
            Log.Error($"whitelist.json parse failed, treating it as empty {_path}: {e.Message}");
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
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            File.WriteAllText(_path, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (Exception e)
        {
            Log.Error($"whitelist.json write failed {_path}: {e.Message}");
        }
    }

    //WhiteListEntry a single whitelist record
    private sealed record WhiteListEntry(Guid Id, string Name);
}
