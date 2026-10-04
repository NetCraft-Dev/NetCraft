using System.Text.Json;

namespace NetCraft.Resources;

//PackMetadataSection 资源包元数据段对应原版 net.minecraft.server.packs.metadata.pack.PackMetadataSection
//解析 pack.mcmeta 根 JSON 的 pack 子对象含 pack_format 与 description
//用 System.Text.Json 对齐 TagFile 模式不走 Codec 路线
public sealed record PackMetadataSection(int PackFormat, string Description)
{
    //FromJson 解析 pack.mcmeta 根 JSON 的 pack 子对象
    //格式 {"pack":{"pack_format":N,"description":"..."}} 缺 pack 节点抛 JsonException
    //description 允许是字符串或聊天组件对象对象时返回 GetRawText 对齐原版
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

//PackMetadataSectionReader 元数据段读取器对应原版 MetadataSectionType
//PackResources 通过 GetRootResource("pack.mcmeta") 暴露此 reader 统一解析
public static class PackMetadataSectionReader
{
    //SectionName 元数据段名对应原版 pack
    public const string SectionName = "pack";

    //Read 从 PackResources 读 pack.mcmeta 并解析返回 null 表示文件不存在
    public static PackMetadataSection? Read(PackResources pack)
    {
        using var stream = pack.GetRootResource("pack.mcmeta");
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return PackMetadataSection.FromJson(reader.ReadToEnd());
    }
}
