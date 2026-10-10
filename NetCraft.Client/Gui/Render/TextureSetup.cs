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
namespace NetCraft.Client.Gui.Render;

//TextureSetup texture binding config, maps to vanilla TextureSetup
//At most 3 texture+sampler pairs participating in batching and sorting
//Equals reference equality used for SortElements batching decisions
public sealed class TextureSetup
{
    public GpuImage? Texture0 { get; }
    public GpuSampler? Sampler0 { get; }
    public GpuImage? Texture1 { get; }
    public GpuSampler? Sampler1 { get; }
    public GpuImage? Texture2 { get; }
    public GpuSampler? Sampler2 { get; }

    private TextureSetup(GpuImage? t0, GpuSampler? s0, GpuImage? t1, GpuSampler? s1, GpuImage? t2, GpuSampler? s2)
    {
        Texture0 = t0; Sampler0 = s0;
        Texture1 = t1; Sampler1 = s1;
        Texture2 = t2; Sampler2 = s2;
    }

    //NoTexture shared singleton for solid-color pipelines with no texture
    public static readonly TextureSetup NoTexture = new(null, null, null, null, null, null);

    //SingleTexture single texture binding
    public static TextureSetup SingleTexture(GpuImage texture, GpuSampler sampler)
        => new(texture, sampler, null, null, null, null);

    //SingleTextureWithLightmap single texture + lightmap two textures
    public static TextureSetup SingleTextureWithLightmap(GpuImage texture, GpuSampler sampler, GpuImage lightmap, GpuSampler lightmapSampler)
        => new(texture, sampler, lightmap, lightmapSampler, null, null);

    public override bool Equals(object? obj)
    {
        if (obj is not TextureSetup other) return false;
        return ReferenceEquals(Texture0, other.Texture0) && ReferenceEquals(Sampler0, other.Sampler0)
            && ReferenceEquals(Texture1, other.Texture1) && ReferenceEquals(Sampler1, other.Sampler1)
            && ReferenceEquals(Texture2, other.Texture2) && ReferenceEquals(Sampler2, other.Sampler2);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Texture0); hash.Add(Sampler0);
        hash.Add(Texture1); hash.Add(Sampler1);
        hash.Add(Texture2); hash.Add(Sampler2);
        return hash.ToHashCode();
    }
}
