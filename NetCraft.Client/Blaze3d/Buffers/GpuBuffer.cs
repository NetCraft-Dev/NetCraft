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
namespace NetCraft.Client.Blaze3d.Buffers;

//GpuBufferUsage buffer usage
//VertexBuffer vertex buffer
//IndexBuffer index buffer
//UniformBuffer shader uniform data
//StagingBuffer staging area for uploading and downloading data
public enum GpuBufferUsage
{
    VertexBuffer,
    IndexBuffer,
    UniformBuffer,
    StagingBuffer
}

//GpuBuffer GPU memory buffer abstraction, corresponds to vanilla blaze3d VertexBuffer/IndexBuffer/UniformBuffer
//Subclasses provide the Upload/Download implementations
public abstract class GpuBuffer : IDisposable
{
    //Size byte size
    public int Size { get; }
    //Usage usage
    public GpuBufferUsage Usage { get; }

    protected GpuBuffer(int size, GpuBufferUsage usage)
    {
        Size = size;
        Usage = usage;
    }

    //Upload uploads a struct array to the GPU
    public abstract void Upload<T>(ReadOnlySpan<T> data) where T : struct;

    //Download downloads data into a span, only applicable to StagingBuffer
    public abstract void Download<T>(Span<T> data) where T : struct;

    public virtual void Dispose() { }
}
