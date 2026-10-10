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

//GpuDescriptorType descriptor type
public enum GpuDescriptorType
{
    UniformBuffer,
    CombinedImageSampler
}

//GpuShaderStageFlags shader stage visibility bitmask
[Flags]
public enum GpuShaderStageFlags
{
    None = 0,
    Vertex = 1,
    Fragment = 2,
    Compute = 4,
    AllGraphics = Vertex | Fragment
}

//GpuDescriptorBinding description of a single descriptor binding
public sealed class GpuDescriptorBinding
{
    public int Binding { get; set; }
    public GpuDescriptorType DescriptorType { get; set; }
    public GpuShaderStageFlags StageFlags { get; set; } = GpuShaderStageFlags.AllGraphics;
    public int DescriptorCount { get; set; } = 1;
}

//GpuDescriptorLayoutDescription descriptor set layout description
public sealed class GpuDescriptorLayoutDescription
{
    public List<GpuDescriptorBinding> Bindings { get; set; } = new();
}

//GpuDescriptorLayout descriptor set layout abstraction
//Subclasses create the underlying layout object (Vulkan VkDescriptorSetLayout)
public abstract class GpuDescriptorLayout : IDisposable
{
    public GpuDescriptorLayoutDescription Description { get; }

    protected GpuDescriptorLayout(GpuDescriptorLayoutDescription description)
    {
        Description = description;
    }

    public virtual void Dispose() { }
}

//GpuDescriptorSet descriptor set instance
//WriteBuffer binds a uniform buffer to the given binding slot
//WriteImage binds a combined image sampler to the given binding slot
public abstract class GpuDescriptorSet : IDisposable
{
    public GpuDescriptorLayout Layout { get; }

    protected GpuDescriptorSet(GpuDescriptorLayout layout)
    {
        Layout = layout;
    }

    //WriteBuffer binds a uniform buffer; range=-1 means the whole buffer
    public abstract void WriteBuffer(int binding, GpuBuffer buffer, int offset = 0, int range = -1);

    //WriteImage binds a combined image sampler
    public abstract void WriteImage(int binding, GpuTexture texture, GpuSampler sampler);

    public virtual void Dispose() { }
}
