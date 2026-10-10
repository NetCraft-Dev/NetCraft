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
namespace NetCraft.Client.Blaze3d.Pipeline;

//UniformDescription uniform description, maps to vanilla UniformDescription record
//TEXEL_BUFFER must specify a GpuFormat; for other types GpuFormat is null
public readonly record struct UniformDescription(string Name, UniformType Type, GpuFormat? GpuFormat)
{
    public UniformDescription(string name, UniformType type) : this(name, type, null)
    {
        if (type == UniformType.TexelBuffer)
            throw new ArgumentException("Texel buffer needs a texture format");
    }

    public UniformDescription(string name, GpuFormat format) : this(name, UniformType.TexelBuffer, format) { }
}

//BindGroupLayout bind group layout, maps to vanilla BindGroupLayout
//Describes a set of sampler and uniform bindings for RenderPipeline to allocate a descriptor set layout at compile time
public sealed class BindGroupLayout
{
    public IReadOnlyList<string> Samplers { get; }
    public IReadOnlyList<UniformDescription> Uniforms { get; }

    private BindGroupLayout(IReadOnlyList<string> samplers, IReadOnlyList<UniformDescription> uniforms)
    {
        Samplers = samplers;
        Uniforms = uniforms;
    }

    public static Builder Create() => new();

    public sealed class Builder
    {
        private readonly List<string> _samplers = new();
        private readonly List<UniformDescription> _uniforms = new();

        public Builder AddSampler(string name)
        {
            _samplers.Add(name);
            return this;
        }

        public Builder AddUniform(string name, UniformType type)
        {
            _uniforms.Add(new UniformDescription(name, type));
            return this;
        }

        public Builder AddUniform(string name, UniformType type, GpuFormat format)
        {
            _uniforms.Add(new UniformDescription(name, type, format));
            return this;
        }

        public BindGroupLayout Build() => new(_samplers, _uniforms);
    }
}
