using System.Text.Json;

namespace NetCraft.Gpu.Sprite;

//GuiMetadataSection .mcmeta 的 gui.scaling 段解析对标原版 GuiMetadataSection
//从 JsonElement 解析 GuiSpriteScaling 三种类型 + Border 双格式 int/object
//.mcmeta 文件根结构 {"gui":{"scaling":{"type":"nine_slice","width":200,"height":20,"border":3}}}
public static class GuiMetadataSection
{
    //Parse 解析.mcmeta 文件 JSON 根节点返回 GuiSpriteScaling
    //找不到 gui.scaling 段返回 Stretch 默认值
    public static GuiSpriteScaling Parse(JsonElement root)
    {
        if (!root.TryGetProperty("gui", out var gui)) return GuiSpriteScaling.Default;
        if (!gui.TryGetProperty("scaling", out var scaling)) return GuiSpriteScaling.Default;
        return ParseScaling(scaling);
    }

    //ParseScaling 按 type 字段分派三种缩放模式
    private static GuiSpriteScaling ParseScaling(JsonElement el)
    {
        if (!el.TryGetProperty("type", out var typeEl)) return GuiSpriteScaling.Default;
        var type = typeEl.GetString();
        return type switch
        {
            "stretch" => new StretchScaling(),
            "tile" => new TileScaling(
                el.GetProperty("width").GetInt32(),
                el.GetProperty("height").GetInt32()),
            "nine_slice" => ParseNineSlice(el),
            _ => GuiSpriteScaling.Default
        };
    }

    //ParseNineSlice 解析 nine_slice 段含 width/height/border/stretch_inner(可选)
    //stretch_inner 缺省时为 false 对标原版 Codec.BOOL.optionalFieldOf("stretch_inner", false)
    private static GuiSpriteScaling ParseNineSlice(JsonElement el)
    {
        int width = el.GetProperty("width").GetInt32();
        int height = el.GetProperty("height").GetInt32();
        var border = ParseBorder(el.GetProperty("border"));
        bool stretchInner = el.TryGetProperty("stretch_inner", out var si) && si.GetBoolean();
        return new NineSliceScaling(width, height, border, stretchInner);
    }

    //ParseBorder 双格式解析 int→Uniform object→四边独立
    //对标原版 Border.CODEC = Codec.either(VALUE_CODEC, RECORD_CODEC)
    //button.png.mcmeta 用 int slider_handle.png.mcmeta 用 object
    private static NineSliceBorder ParseBorder(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number)
            return NineSliceBorder.Uniform(el.GetInt32());
        if (el.ValueKind == JsonValueKind.Object)
            return new NineSliceBorder(
                el.GetProperty("left").GetInt32(),
                el.GetProperty("top").GetInt32(),
                el.GetProperty("right").GetInt32(),
                el.GetProperty("bottom").GetInt32());
        return NineSliceBorder.Uniform(0);
    }
}
