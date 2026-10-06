using System.Text.Json;
using NetCraft.Registry;

namespace NetCraft.Tags;

//TagFile tag file format, maps to vanilla net.minecraft.tags.TagFile
//Contains a replace flag and a values list
//JSON format aligns with vanilla {"replace": bool, "values": ["id", "#tag", "!id", {"id": "...", "required": false}]}
public sealed record TagFile(bool Replace, List<TagEntry> Entries)
{
    //FromJson parses a TagFile from a JSON string
    //The field name is values, vanilla data packs always use that name, reading entries would parse every tag into an empty collection
    public static TagFile FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var replace = root.TryGetProperty("replace", out var r) && r.GetBoolean();
        var entries = new List<TagEntry>();
        if (root.TryGetProperty("values", out var e))
        {
            foreach (var entry in e.EnumerateArray())
            {
                entries.Add(ReadEntry(entry));
            }
        }
        return new TagFile(replace, entries);
    }

    //ReadEntry reads one tag entry, strings use the prefix encoding while objects take the id/tag/required fields
    //required defaults to true in vanilla, same meaning as the ! prefix in this project (a missing element must be reported)
    private static TagEntry ReadEntry(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return TagEntry.FromString(element.GetString()!);
        var id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var isTag = element.TryGetProperty("tag", out var tagElement) && tagElement.GetBoolean();
        var required = !element.TryGetProperty("required", out var requiredElement) || requiredElement.GetBoolean();
        return new TagEntry(Identifier.Parse(id), required, isTag);
    }

    //ToJson serializes to a JSON string
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("replace", Replace);
            writer.WriteStartArray("values");
            foreach (var entry in Entries)
            {
                writer.WriteStringValue(entry.AsString());
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
