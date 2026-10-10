using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
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

namespace NetCraft.Client.Blaze3d.Systems;

//GpuDevice GPU logical device, corresponds to the vanilla RenderSystem abstraction
//Provides command buffer allocation and resource creation entry points
public abstract class GpuDevice : IDisposable
{
    public GpuContext Context { get; }
    //ShaderManager declarative pipeline shader load/compile entry; stage 8 completes the FromDeclaration shader loading
    public ShaderManager ShaderManager { get; }

    //Limits GPU hardware limits, maps to vanilla device.getDeviceInfo().limits()
    //Subclasses query the backend's real values; Vulkan uses VkPhysicalDeviceLimits.maxImageDimension2D
    //The Empty/Mock backends use 4096 as a default placeholder so they compile and run without Vulkan
    public abstract DeviceLimits Limits { get; }

    //SupportsGpuRendering whether recording GPU render commands is supported; true for the Vulkan backend, false for Empty/Mock
    //ItemItemAtlas.DrawToSlot uses this to decide between GPU rendering and CPU-only vertex generation
    public virtual bool SupportsGpuRendering => false;

    protected GpuDevice(GpuContext context)
    {
        Context = context;
        ShaderManager = new ShaderManager();
    }

    //CreateCommandBuffer creates a command buffer for recording render commands
    public abstract GpuCommandBuffer CreateCommandBuffer();

    //CreateRenderPipeline creates a render pipeline
    public abstract CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription description);

    //CreateBuffer creates a GPU buffer
    public abstract GpuBuffer CreateBuffer(int size, GpuBufferUsage usage);

    //CreateHostVisibleBuffer creates a host-visible memory buffer, suited to vertex/index buffers updated every frame
    //The default falls back to CreateBuffer using device-local+staging; subclasses may override to provide the host-visible optimization
    //Host-visible uses map+memcpy to avoid the QueueSubmit+QueueWaitIdle synchronization cost of staging
    public virtual GpuBuffer CreateHostVisibleBuffer(int size, GpuBufferUsage usage)
        => CreateBuffer(size, usage);

    //CreateImage creates a GPU image/texture
    public abstract GpuImage CreateImage(GpuImageDescription desc);

    //CreateShader creates a SPIR-V shader module
    public abstract GpuShader CreateShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main");

    //CreateDescriptorLayout creates a descriptor set layout
    public abstract GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription description);

    //AllocateDescriptorSet allocates a descriptor set from the internal pool
    public abstract GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout layout);

    //CreateSampler creates a texture sampler
    public abstract GpuSampler CreateSampler(GpuSamplerDescription description);

    //CreateCommandEncoder creates a command encoder to record copy/render pass commands
    //Legacy backends throw NotSupportedException; the Vulkan backend overrides it
    public virtual ICommandEncoder CreateCommandEncoder() =>
        throw new NotSupportedException("The current backend does not support ICommandEncoder");

    //PrecompilePipeline compiles a declarative RenderPipeline into a CompiledRenderPipeline
    //The default converts the declaration to a RenderPipelineDescription, compiles the descriptor layout, then CreateRenderPipeline
    //Subclasses may override to hook into PipelineCache and cache the compiled artifact
    public virtual CompiledRenderPipeline PrecompilePipeline(RenderPipeline declaration)
    {
        var description = RenderPipelineDescription.FromDeclaration(declaration, ShaderManager);
        foreach (var layoutDesc in description.DescriptorLayoutDescriptions)
            description.DescriptorLayouts.Add(CreateDescriptorLayout(layoutDesc));
        description.DescriptorLayoutDescriptions.Clear();
        return CreateRenderPipeline(description);
    }

    //PrecompilePipeline legacy RenderPipelineDescription overload, callers do not cache
    public virtual CompiledRenderPipeline PrecompilePipeline(RenderPipelineDescription description) =>
        CreateRenderPipeline(description);

    public virtual void Dispose() { }
}
