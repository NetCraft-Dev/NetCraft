using System.Numerics;
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

namespace NetCraft.Client.Blaze3d.Systems;

//GpuLoadOp attachment load strategy Clear clears Load preserves previous content
//ItemAtlas uses Load for multi-slot rendering to preserve other slots' content, Clear for single-slot rendering
public enum GpuLoadOp
{
    Clear,
    Load
}

//GpuImageLayout backend-agnostic image layout enum for TransitionImageLayout
//The Vulkan backend maps these to ImageLayout: ColorAttachment render attachment ShaderReadOnly texture sampling
public enum GpuImageLayout
{
    ColorAttachment,
    ShaderReadOnly,
    TransferDst,
    TransferSrc
}

//ICommandEncoder command encoder, maps to vanilla CommandEncoder
//Records copy/clear/render pass commands and submits them to the GPU queue on Submit
//Replaces the mixed recording of the legacy GpuCommandBuffer, separating command encoding from render passes
public interface ICommandEncoder : IDisposable
{
    //CreateRenderPass creates a render pass; the pipeline provides the RenderPass and framebuffer
    //Stage 3 introduced dynamic rendering, decoupling RenderPass from the pipeline and restoring the vanilla interface
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuTexture colorImage, Vector4 clearColor);
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuTexture colorImage, Vector4 clearColor, GpuTexture depthImage, float clearDepth);
    //CreateRenderPass overload with colorLoadOp; Load preserves previous color content for multi-slot shared atlases
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuTexture colorImage, Vector4 clearColor, GpuTexture depthImage, float clearDepth, GpuLoadOp colorLoadOp);
    //CopyBuffer copies the source buffer to the destination buffer
    void CopyBuffer(GpuBuffer src, GpuBuffer dst, ulong srcOffset, ulong dstOffset, ulong size);
    //WriteToTexture uploads pixel data to an image via staging
    void WriteToTexture(GpuTexture dst, ReadOnlySpan<byte> data, int dstX, int dstY, int width, int height);
    //TransitionImageLayout records an image layout transition into the current command buffer, for switching an offscreen render to a sampling layout
    //VulkanImage.TransitionLayout internally checks currentLayout==newLayout and safely skips redundant transitions
    void TransitionImageLayout(GpuTexture image, GpuImageLayout newLayout);
    //Submit submits all recorded commands to the GPU queue and waits for completion
    void Submit();
    //SubmitAsync submits commands to the GPU queue without waiting, for PIP double-buffered async rendering
    //The caller must WaitForCompletion before reusing this encoder to ensure the GPU is done
    //The staging buffer is released lazily at WaitForCompletion
    void SubmitAsync() => throw new NotSupportedException("The current backend does not support async Submit");
    //WaitForCompletion waits for the GPU commands submitted by SubmitAsync to finish
    //A no-op if SubmitAsync was never called, safe to skip when reusing the encoder the first time
    void WaitForCompletion() => throw new NotSupportedException("The current backend does not support async Submit");
    //BeginRecording restarts command recording so the encoder can be reused across frames
    //The first call is idempotent (already begun in the constructor); later calls do ResetCommandBuffer + BeginCommandBuffer
    //Must be called after WaitForCompletion to ensure the GPU no longer uses the command buffer
    void BeginRecording() => throw new NotSupportedException("The current backend does not support encoder reuse");
}
