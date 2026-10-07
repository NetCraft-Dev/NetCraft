using System.Text.Json;
using NetCraft.Logging;

namespace NetCraft.Game.Server;

//StoredJsonList base class for list file read/write, maps to vanilla net.minecraft.server.players.StoredUserList
//Handles loading and persisting the JSON array; per-record field read/write is implemented by subclasses
//A missing file creates an empty list and a parse failure is treated as empty without blocking startup, the same strategy as OpList
public abstract class StoredJsonList<TEntry> where TEntry : class
{
    private readonly string _path;
    private readonly List<TEntry> _entries = new();

    protected StoredJsonList(string path)
    {
        _path = path;
        Load();
    }

    //Path the list file path
    public string Path => _path;

    //Count the number of list entries
    public int Count => _entries.Count;

    //Entries a read-only view of entries, listed by the banlist command
    public IReadOnlyList<TEntry> Entries => _entries;

    //AddEntry appends a record and persists it
    protected void AddEntry(TEntry entry)
    {
        _entries.Add(entry);
        Save();
    }

    //RemoveEntry removes the first matching record and persists it; returns whether one was hit
    protected bool RemoveEntry(Predicate<TEntry> match)
    {
        var index = _entries.FindIndex(match);
        if (index < 0) return false;
        _entries.RemoveAt(index);
        Save();
        return true;
    }

    //ReadEntry reads a record from a JSON object; a missing field returns null meaning skip that entry
    protected abstract TEntry? ReadEntry(JsonElement element);

    //WriteEntry writes a record as a JSON object
    protected abstract void WriteEntry(Utf8JsonWriter writer, TEntry entry);

    //FileName the list file name, for logging
    private string FileName => System.IO.Path.GetFileName(_path);

    //Load reads the list; a missing file creates an empty list and a parse failure is treated as empty
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
