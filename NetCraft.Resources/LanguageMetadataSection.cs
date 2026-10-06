using System.Text.Json;

namespace NetCraft.Resources;

//LanguageInfo, display info for a language, maps to vanilla net.minecraft.locale.LanguageInfo
//region is the region name, name is the language's own name, bidirectional whether it is written right to left
public sealed record LanguageInfo(string Region, string Name, bool Bidirectional);

//LanguageMetadataSection, language metadata section, maps to vanilla net.minecraft.server.packs.metadata.language.LanguageMetadataSection
//Parses the language sub-object of the pack.mcmeta root JSON, keys are language codes and values are display info
//The language selection screen lists entries from this, sharing the same pack.mcmeta file as the pack section
public sealed class LanguageMetadataSection
{
    //SectionName is the section name, maps to vanilla language
    public const string SectionName = "language";

    //FileName is the metadata file name, shared with the pack section
    private const string FileName = "pack.mcmeta";

    //Languages maps language codes to display info, empty when there is no language section
    public IReadOnlyDictionary<string, LanguageInfo> Languages { get; }

    //Empty, an empty table, a placeholder before metadata is loaded
    public static LanguageMetadataSection Empty { get; } =
        new(new Dictionary<string, LanguageInfo>(StringComparer.Ordinal));

    private LanguageMetadataSection(Dictionary<string, LanguageInfo> languages) => Languages = languages;

    //FromJson parses the pack.mcmeta root JSON, returns an empty table instead of throwing when the language section is missing
    public static LanguageMetadataSection FromJson(string json)
    {
        var languages = new Dictionary<string, LanguageInfo>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(SectionName, out var section)
            || section.ValueKind != JsonValueKind.Object)
        {
            return new LanguageMetadataSection(languages);
        }

        foreach (var entry in section.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            var value = entry.Value;
            var region = value.TryGetProperty("region", out var r) ? r.GetString() ?? string.Empty : string.Empty;
            var name = value.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            var bidirectional = value.TryGetProperty("bidirectional", out var b) && b.ValueKind == JsonValueKind.True;
            languages[entry.Name] = new LanguageInfo(region, name, bidirectional);
        }

        return new LanguageMetadataSection(languages);
    }

    //Read reads pack.mcmeta from a resource pack, returns null when the file does not exist
    public static LanguageMetadataSection? Read(PackResources pack)
    {
        using var stream = pack.GetRootResource(FileName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    //ReadAll merges the language sections of all packs, higher priority packs override lower ones
    public static LanguageMetadataSection ReadAll(ResourceManager manager)
    {
        var languages = new Dictionary<string, LanguageInfo>(StringComparer.Ordinal);
        foreach (var pack in manager.Packs)
        {
            var section = Read(pack.Resources);
            if (section is null) continue;
            foreach (var (code, info) in section.Languages) languages[code] = info;
        }
        return new LanguageMetadataSection(languages);
    }
}
