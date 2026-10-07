using System.Text.Json;

namespace NetCraft.Gpu.Sprite;

//GuiMetadataSection parser for the gui.scaling section of .mcmeta, maps to vanilla GuiMetadataSection
//Parses the three GuiSpriteScaling types + the dual-format Border int/object from a JsonElement
//The .mcmeta root structure {"gui":{"scaling":{"type":"nine_slice","width":200,"height":20,"border":3}}}
public static class GuiMetadataSection
{
    //Parse parses the .mcmeta JSON root node and returns a GuiSpriteScaling
    //Returns the Stretch default when no gui.scaling section is found
    public static GuiSpriteScaling Parse(JsonElement root)
    {
        if (!root.TryGetProperty("gui", out var gui)) return GuiSpriteScaling.Default;
        if (!gui.TryGetProperty("scaling", out var scaling)) return GuiSpriteScaling.Default;
        return ParseScaling(scaling);
    }

    //ParseScaling dispatches the three scaling modes by the type field
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

    //ParseNineSlice parses the nine_slice section with width/height/border/stretch_inner (optional)
    //stretch_inner defaults to false, maps to vanilla Codec.BOOL.optionalFieldOf("stretch_inner", false)
    private static GuiSpriteScaling ParseNineSlice(JsonElement el)
    {
        int width = el.GetProperty("width").GetInt32();
        int height = el.GetProperty("height").GetInt32();
        var border = ParseBorder(el.GetProperty("border"));
        bool stretchInner = el.TryGetProperty("stretch_inner", out var si) && si.GetBoolean();
        return new NineSliceScaling(width, height, border, stretchInner);
    }

    //ParseBorder dual-format parsing int→Uniform object→independent per side
    //maps to vanilla Border.CODEC = Codec.either(VALUE_CODEC, RECORD_CODEC)
    //button.png.mcmeta uses int slider_handle.png.mcmeta uses object
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
