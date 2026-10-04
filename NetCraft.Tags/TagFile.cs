using System.Text.Json;
using NetCraft.Registry;

namespace NetCraft.Tags;

//TagFile 标签文件格式对应原版 net.minecraft.tags.TagFile
//含 replace 标志和 values 列表
//JSON 格式对齐原版 {"replace": bool, "values": ["id", "#tag", "!id", {"id": "...", "required": false}]}
public sealed record TagFile(bool Replace, List<TagEntry> Entries)
{
    //FromJson 从 JSON 字符串解析 TagFile
    //字段名是 values 原版数据包全用这个名字 读 entries 会让所有标签解析成空集合
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

    //ReadEntry 读一个标签项 字符串走前缀编码 对象取 id/tag/required 三个字段
    //required 原版缺省是真 与本项目的 ! 前缀同义(元素缺失要报错)
    private static TagEntry ReadEntry(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return TagEntry.FromString(element.GetString()!);
        var id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var isTag = element.TryGetProperty("tag", out var tagElement) && tagElement.GetBoolean();
        var required = !element.TryGetProperty("required", out var requiredElement) || requiredElement.GetBoolean();
        return new TagEntry(Identifier.Parse(id), required, isTag);
    }

    //ToJson 序列化为 JSON 字符串
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
