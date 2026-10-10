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

//IFontResourceAccessor font resource access abstraction replacing vanilla ResourceManager
//Loader reads PNG/TTF/hex resources through this interface when loading a provider
//The F4 FontManager provides an implementation that reads from the assets directory
public interface IFontResourceAccessor
{
    //OpenResource opens a resource stream by namespace:path, or null if absent
    //e.g. minecraft:font/ascii.png → assets/minecraft/textures/font/ascii.png
    Stream? OpenResource(string identifier);
}

//IGlyphProviderDefinition glyph provider definition, maps to vanilla GlyphProviderDefinition
//The providers[] of font.json parse into definition+filter(Conditional)
//definition is either a Loader (direct load) or a Reference (references another provider)
public interface IGlyphProviderDefinition
{
    GlyphProviderType Type { get; }

    //IsReference distinguishes a Loader from a Reference; vanilla uses Either<Loader,Reference>
    //When true, AsReference returns the reference; when false, AsLoader returns the loader
    bool IsReference { get; }
    IGlyphProviderDefinition.ILoader? AsLoader => null;
    IGlyphProviderDefinition.Reference? AsReference => null;

    //ILoader loader loads a provider, needs resource access and returns null on failure
    public interface ILoader
    {
        IGlyphProvider? Load(IFontResourceAccessor resources);
    }

    //Reference references another provider looked up by Identifier, maps to vanilla GlyphProviderDefinition.Reference
    public sealed class Reference
    {
        public string Id { get; }
        public Reference(string id) => Id = id;
    }

    //Conditional conditional definition holding definition+filter, a parse-stage product
    //maps to vanilla GlyphProviderDefinition.Conditional
    public sealed class Conditional
    {
        public IGlyphProviderDefinition Definition { get; }
        public FontOptionFilter Filter { get; }

        public Conditional(IGlyphProviderDefinition definition, FontOptionFilter filter)
        {
            Definition = definition;
            Filter = filter;
        }
    }
}
