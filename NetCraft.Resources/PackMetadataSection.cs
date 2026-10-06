using System.Text.Json;

namespace NetCraft.Resources;

//PackMetadataSection, resource pack metadata section, maps to vanilla net.minecraft.server.packs.metadata.pack.PackMetadataSection
//Parses the pack sub-object of the pack.mcmeta root JSON, holding pack_format and description
//Uses System.Text.Json following the TagFile pattern instead of the Codec route
public sealed record PackMetadataSection(int PackFormat, string Description)
{
    //FromJson parses the pack sub-object of the pack.mcmeta root JSON
    //Format {"pack":{"pack_format":N,"description":"..."}}, a missing pack node throws JsonException
    //description may be a string or a chat component object, an object returns GetRawText to align with vanilla
    public static PackMetadataSection FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("pack", out var pack))
            throw new JsonException("Missing 'pack' node in pack.mcmeta");
        int format = pack.TryGetProperty("pack_format", out var pf) ? pf.GetInt32() : 0;
        string desc = pack.TryGetProperty("description", out var d)
            ? (d.ValueKind == JsonValueKind.String ? d.GetString()! : d.GetRawText())
            : string.Empty;
        return new PackMetadataSection(format, desc);
    }
}

//PackMetadataSectionReader, metadata section reader, maps to vanilla MetadataSectionType
//PackResources exposes this reader through GetRootResource("pack.mcmeta") for unified parsing
public static class PackMetadataSectionReader
{
    //SectionName is the metadata section name, maps to vanilla pack
    public const string SectionName = "pack";

    //Read reads and parses pack.mcmeta from PackResources, null means the file does not exist
    public static PackMetadataSection? Read(PackResources pack)
    {
        using var stream = pack.GetRootResource("pack.mcmeta");
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return PackMetadataSection.FromJson(reader.ReadToEnd());
    }
}
