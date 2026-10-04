using System.Text.Json;

namespace NetCraft.Gpu.Font;

//FontProviderDefinitionLoader font/*.json 解析器
//解析 providers 数组按 type 字段分发到对应 Definition 构造 IGlyphProvider
//reference 类型递归加载引用的 json 循环引用保护
//filter 字段包装 Conditional 按当前 FontOption 集合激活
public static class FontProviderDefinitionLoader
{
    //Load 解析 font json 流返回 Conditional 列表递归展开 reference
    public static List<IGlyphProvider.Conditional> Load(Stream json, IFontResourceAccessor resources)
    {
        using var doc = JsonDocument.Parse(json);
        return LoadDocument(doc.RootElement, resources, new HashSet<string>());
    }

    private static List<IGlyphProvider.Conditional> LoadDocument(JsonElement root, IFontResourceAccessor resources, HashSet<string> visited)
    {
        var result = new List<IGlyphProvider.Conditional>();
        if (!root.TryGetProperty("providers", out var providers)) return result;
        foreach (var p in providers.EnumerateArray())
        {
            var list = LoadProvider(p, resources, visited);
            result.AddRange(list);
        }
        return result;
    }

    //LoadProvider 返回列表因 reference 递归展开可能产生多个 provider
    private static List<IGlyphProvider.Conditional> LoadProvider(JsonElement elem, IFontResourceAccessor resources, HashSet<string> visited)
    {
        if (!elem.TryGetProperty("type", out var typeElem)) return new();
        var type = typeElem.GetString();
        var filter = LoadFilter(elem);
        return type switch
        {
            "reference" => LoadReference(elem, filter, resources, visited),
            "ttf" => LoadTtf(elem, filter, resources),
            "space" => LoadSpace(elem, filter),
            "bitmap" => LoadBitmap(elem, filter, resources),
            "unihex" => LoadUnihex(elem, filter, resources),
            _ => new()
        };
    }

    //LoadReference 递归加载引用的 font json
    //id 格式 minecraft:include/space → 加载 minecraft:font/include/space.json
    //外层 filter 覆盖所有递归结果的 filter 对齐原版 GlyphProviderDefinition.Conditional 合并语义
    private static List<IGlyphProvider.Conditional> LoadReference(JsonElement elem, FontOptionFilter filter, IFontResourceAccessor resources, HashSet<string> visited)
    {
        var id = elem.GetProperty("id").GetString() ?? "";
        var parts = id.Split(':', 2);
        var ns = parts.Length > 1 ? parts[0] : "minecraft";
        var path = parts.Length > 1 ? parts[1] : id;
        var resourceIdentifier = $"{ns}:font/{path}.json";

        if (!visited.Add(resourceIdentifier)) return new();

        var stream = resources.OpenResource(resourceIdentifier);
        if (stream == null) return new();
        using (stream)
        {
            using var doc = JsonDocument.Parse(stream);
            var result = LoadDocument(doc.RootElement, resources, visited);
            //外层 filter 覆盖所有递归结果
            return result.Select(c => new IGlyphProvider.Conditional(c.Provider, filter)).ToList();
        }
    }

    //LoadTtf 解析 ttf 类型 provider
    //file 必填 size 默认 11.0 oversample 默认 1.0 shift 默认 NONE skip 默认空串
    private static List<IGlyphProvider.Conditional> LoadTtf(JsonElement elem, FontOptionFilter filter, IFontResourceAccessor resources)
    {
        var file = elem.GetProperty("file").GetString() ?? "";
        var size = elem.TryGetProperty("size", out var s) ? s.GetSingle() : 11.0f;
        var oversample = elem.TryGetProperty("oversample", out var o) ? o.GetSingle() : 1.0f;
        var shift = LoadShift(elem);
        var skip = elem.TryGetProperty("skip", out var sk) ? sk.GetString() ?? "" : "";

        var def = new TtfDefinition(file, size, oversample, shift, skip);
        var provider = def.AsLoader?.Load(resources);
        if (provider == null) return new();
        return new() { new(provider, filter) };
    }

    //LoadSpace 解析 space 类型 provider
    //advances 是 codepoint→advance 映射 JSON 键是字符（System.Text.Json 自动解码 \u 转义）
    private static List<IGlyphProvider.Conditional> LoadSpace(JsonElement elem, FontOptionFilter filter)
    {
        var advances = new Dictionary<int, float>();
        if (elem.TryGetProperty("advances", out var adv) && adv.ValueKind == JsonValueKind.Object)
        {
            foreach (var pair in adv.EnumerateObject())
            {
                var cp = ParseCodepoint(pair.Name);
                if (pair.Value.ValueKind == JsonValueKind.Number)
                    advances[cp] = pair.Value.GetSingle();
            }
        }
        var provider = new SpaceGlyphProvider(advances);
        return new() { new(provider, filter) };
    }

    //LoadBitmap 解析 bitmap 类型 provider
    //file 必填 height 默认 8 ascent 必填 chars 必填字符串数组每行 codepoints
    //file 路径 minecraft:font/xxx.png 由 AssetsFontResourceAccessor 映射到 assets
    private static List<IGlyphProvider.Conditional> LoadBitmap(JsonElement elem, FontOptionFilter filter, IFontResourceAccessor resources)
    {
        var file = elem.GetProperty("file").GetString() ?? "";
        var height = elem.TryGetProperty("height", out var h) ? h.GetInt32() : 8;
        var ascent = elem.GetProperty("ascent").GetInt32();
        var charsList = new List<string>();
        if (elem.TryGetProperty("chars", out var chars) && chars.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in chars.EnumerateArray())
                charsList.Add(line.GetString() ?? "");
        }
        var def = new BitmapDefinition(file, height, ascent, charsList.ToArray());
        var provider = def.AsLoader?.Load(resources);
        if (provider == null) return new();
        return new() { new(provider, filter) };
    }

    //LoadUnihex 解析 unihex 类型 provider
    //hex_file 必填指向 zip 打包的 .hex 文件
    //size_overrides 字段原版用于覆盖指定 codepoint 范围的尺寸计算 F5 暂不实现
    private static List<IGlyphProvider.Conditional> LoadUnihex(JsonElement elem, FontOptionFilter filter, IFontResourceAccessor resources)
    {
        var hexFile = elem.GetProperty("hex_file").GetString() ?? "";
        var def = new UnihexDefinition(hexFile);
        var provider = def.AsLoader?.Load(resources);
        if (provider == null) return new();
        return new() { new(provider, filter) };
    }

    //ParseCodepoint 解析 JSON 键字符串为 codepoint
    //System.Text.Json 已解码 \u 转义取首个 rune
    private static int ParseCodepoint(string s)
    {
        foreach (var rune in s.EnumerateRunes())
            return rune.Value;
        return 0;
    }

    //LoadShift 解析 shift 字段（[x, y] 数组）对标原版 Shift.CODEC
    private static Shift LoadShift(JsonElement elem)
    {
        if (!elem.TryGetProperty("shift", out var shift) || shift.ValueKind != JsonValueKind.Array) return Shift.None;
        var arr = shift.EnumerateArray().ToArray();
        if (arr.Length < 2) return Shift.None;
        return new Shift(arr[0].GetSingle(), arr[1].GetSingle());
    }

    //LoadFilter 解析 filter 字段（FontOption→bool 条件映射）
    //无 filter 默认 AlwaysPass value=true 表示要求该 option 激活 false 表示要求未激活
    private static FontOptionFilter LoadFilter(JsonElement elem)
    {
        if (!elem.TryGetProperty("filter", out var filter) || filter.ValueKind != JsonValueKind.Object)
            return FontOptionFilter.AlwaysPass;
        var conditions = new Dictionary<FontOption, bool>();
        foreach (var pair in filter.EnumerateObject())
        {
            FontOption? option = pair.Name switch
            {
                "uniform" => FontOption.Uniform,
                "alt" => FontOption.Alt,
                "illageralt" => FontOption.IllagerAlt,
                _ => null
            };
            if (option == null) continue;
            if (pair.Value.ValueKind == JsonValueKind.False)
                conditions[option] = false;
            else if (pair.Value.ValueKind == JsonValueKind.True)
                conditions[option] = true;
        }
        return new FontOptionFilter(conditions);
    }
}
