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
namespace NetCraft.Client.Render;

//ShaderDefines shader compile-time macro definitions, maps to vanilla ShaderDefines
//values is the key->string value macros; flags is the set of valueless flags
//The builder chains definitions, then Build returns an immutable snapshot
public sealed class ShaderDefines
{
    private readonly Dictionary<string, string> _values;
    private readonly HashSet<string> _flags;

    public IReadOnlyDictionary<string, string> Values => _values;
    public IReadOnlySet<string> Flags => _flags;

    private ShaderDefines(Dictionary<string, string> values, HashSet<string> flags)
    {
        _values = values;
        _flags = flags;
    }

    public static Builder NewBuilder() => new();

    public sealed class Builder
    {
        private readonly Dictionary<string, string> _values = new();
        private readonly HashSet<string> _flags = new();

        //Define valueless flag
        public Builder Define(string key)
        {
            _flags.Add(key);
            return this;
        }

        //Define integer-valued macro
        public Builder Define(string key, int value)
        {
            _values[key] = value.ToString();
            return this;
        }

        //Define float-valued macro
        public Builder Define(string key, float value)
        {
            _values[key] = value.ToString("R");
            return this;
        }

        //Define string-valued macro
        public Builder Define(string key, string value)
        {
            _values[key] = value;
            return this;
        }

        public ShaderDefines Build() => new(_values, _flags);
    }
}
