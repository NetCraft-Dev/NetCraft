using System.Collections.Concurrent;
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

//FontSet maps to vanilla FontSet, a single font set
//Holds the provider chain and looks up codepoints in order, returning the first hit as IUnbakedGlyph
//Vanilla FontSet.getGlyph returns BakedGlyph holding a wrappedStitcher for lazy Bake
//The F3 stage does not wire the Stitcher bakery yet, the caller handles it; refactored once F6 injects the Stitcher
public sealed class FontSet : IDisposable
{
    private List<IGlyphProvider.Conditional> _allProviders = new();
    private List<IGlyphProvider> _activeProviders = new();
    private readonly ConcurrentDictionary<int, IUnbakedGlyph?> _cache = new();

    public FontSet() { }

    //Reload receives the full providers list and the currently active FontOption set
    //maps to vanilla reload(providers, options)
    public void Reload(IEnumerable<IGlyphProvider.Conditional> providers, IReadOnlySet<FontOption> options)
    {
        _allProviders = providers.ToList();
        Reload(options);
    }

    //Reload only re-selects active providers by options
    //maps to vanilla reload(options)
    public void Reload(IReadOnlySet<FontOption> options)
    {
        _activeProviders = _allProviders
            .Where(c => c.Filter.Apply(options))
            .Select(c => c.Provider)
            .ToList();
        _cache.Clear();
    }

    //ActiveProviders exposes the active provider list for F4/F6 to build the glyphsByWidth index
    public IReadOnlyList<IGlyphProvider> ActiveProviders => _activeProviders;

    //GetGlyph looks up the first matching codepoint in provider order
    //maps to a simplified vanilla computeGlyphInfo, dropping the fishy advance check and the nonFishy double cache
    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        return _cache.GetOrAdd(codepoint, ComputeGlyph);
    }

    private IUnbakedGlyph? ComputeGlyph(int codepoint)
    {
        foreach (var provider in _activeProviders)
        {
            var glyph = provider.GetGlyph(codepoint);
            if (glyph != null)
                return glyph;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var p in _allProviders)
            p.Provider.Dispose();
        _allProviders.Clear();
        _activeProviders.Clear();
        _cache.Clear();
    }
}
