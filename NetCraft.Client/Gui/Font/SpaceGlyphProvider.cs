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

//SpaceGlyphProvider maps to vanilla SpaceProvider
//Spaces have only advance, no bitmap; the advances Map<int,float> converts to an EmptyGlyph dictionary
//The advances field of a "space" provider in font/*.json defines the width of each space character
//e.g. U+0020 regular space advance=4.0, U+00A0 no-break space advance=4.0
public sealed class SpaceGlyphProvider : IGlyphProvider
{
    private readonly Dictionary<int, EmptyGlyph> _glyphs;

    public SpaceGlyphProvider(IReadOnlyDictionary<int, float> advances)
    {
        _glyphs = new Dictionary<int, EmptyGlyph>(advances.Count);
        foreach (var (codepoint, advance) in advances)
            _glyphs[codepoint] = new EmptyGlyph(advance);
    }

    public IReadOnlySet<int> GetSupportedGlyphs() => _supported ??= new HashSet<int>(_glyphs.Keys);

    private HashSet<int>? _supported;

    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        return _glyphs.TryGetValue(codepoint, out var glyph) ? glyph : null;
    }

    public void Dispose() { }
}
