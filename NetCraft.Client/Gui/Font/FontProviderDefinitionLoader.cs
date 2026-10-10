using System.Text.Json;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Gui.Font;

//FontProviderDefinitionLoader parser for font/*.json
//Parses the providers array and dispatches by the type field to the matching Definition to build an IGlyphProvider
//The reference type recursively loads the referenced json with cycle protection
//The filter field wraps into Conditional and is activated by the current FontOption set
public static class FontProviderDefinitionLoader
{
    //Load parses a font json stream and returns a Conditional list, recursively expanding references
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

    //LoadProvider returns a list because recursive reference expansion may yield multiple providers
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

    //LoadReference recursively loads a referenced font json
    //id format minecraft:include/space → loads minecraft:font/include/space.json
    //The outer filter overrides the filter of all recursive results, matching the merge semantics of vanilla GlyphProviderDefinition.Conditional
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
            //The outer filter overrides all recursive results
            return result.Select(c => new IGlyphProvider.Conditional(c.Provider, filter)).ToList();
        }
    }

    //LoadTtf parses a ttf provider
    //file is required, size defaults to 11.0, oversample to 1.0, shift to NONE, skip to an empty string
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

    //LoadSpace parses a space provider
    //advances is a codepoint→advance map; the JSON keys are characters (System.Text.Json auto-decodes \u escapes)
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

    //LoadBitmap parses a bitmap provider
    //file is required, height defaults to 8, ascent is required, chars is a required string array with codepoints per row
    //The minecraft:font/xxx.png file path is mapped to assets by AssetsFontResourceAccessor
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

    //LoadUnihex parses a unihex provider
    //hex_file is required and points to a zip-packed .hex file
    //Vanilla uses the size_overrides field to override size computation for a codepoint range; not implemented in F5
    private static List<IGlyphProvider.Conditional> LoadUnihex(JsonElement elem, FontOptionFilter filter, IFontResourceAccessor resources)
    {
        var hexFile = elem.GetProperty("hex_file").GetString() ?? "";
        var def = new UnihexDefinition(hexFile);
        var provider = def.AsLoader?.Load(resources);
        if (provider == null) return new();
        return new() { new(provider, filter) };
    }

    //ParseCodepoint parses a JSON key string into a codepoint
    //System.Text.Json has already decoded \u escapes; takes the first rune
    private static int ParseCodepoint(string s)
    {
        foreach (var rune in s.EnumerateRunes())
            return rune.Value;
        return 0;
    }

    //LoadShift parses the shift field ([x, y] array), maps to vanilla Shift.CODEC
    private static Shift LoadShift(JsonElement elem)
    {
        if (!elem.TryGetProperty("shift", out var shift) || shift.ValueKind != JsonValueKind.Array) return Shift.None;
        var arr = shift.EnumerateArray().ToArray();
        if (arr.Length < 2) return Shift.None;
        return new Shift(arr[0].GetSingle(), arr[1].GetSingle());
    }

    //LoadFilter parses the filter field (FontOption→bool condition map)
    //Without a filter it defaults to AlwaysPass; value=true requires the option to be active, false requires it inactive
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
