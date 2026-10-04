using System.Text.Json;

namespace NetCraft.Resources;

//LanguageInfo 语言的展示信息对应原版 net.minecraft.locale.LanguageInfo
//region 地区名 name 该语言自称的名字 bidirectional 是否从右向左书写
public sealed record LanguageInfo(string Region, string Name, bool Bidirectional);

//LanguageMetadataSection 语言元数据段对应原版 net.minecraft.server.packs.metadata.language.LanguageMetadataSection
//解析 pack.mcmeta 根 JSON 的 language 子对象 键为语言码 值为展示信息
//语言选择界面据此列出可选条目 与 pack 段共用同一个 pack.mcmeta 文件
public sealed class LanguageMetadataSection
{
    //SectionName 段名对应原版 language
    public const string SectionName = "language";

    //FileName 元数据文件名与 pack 段共用
    private const string FileName = "pack.mcmeta";

    //Languages 语言码到展示信息的映射 无 language 段时为空表
    public IReadOnlyDictionary<string, LanguageInfo> Languages { get; }

    //Empty 空表 尚未加载元数据时的占位
    public static LanguageMetadataSection Empty { get; } =
        new(new Dictionary<string, LanguageInfo>(StringComparer.Ordinal));

    private LanguageMetadataSection(Dictionary<string, LanguageInfo> languages) => Languages = languages;

    //FromJson 解析 pack.mcmeta 根 JSON 缺 language 段返回空表不抛
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

    //Read 从资源包读 pack.mcmeta 文件不存在返回 null
    public static LanguageMetadataSection? Read(PackResources pack)
    {
        using var stream = pack.GetRootResource(FileName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    //ReadAll 合并全部资源包的 language 段 优先级高的包覆盖低的
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
