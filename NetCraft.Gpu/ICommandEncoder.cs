using System.Numerics;

namespace NetCraft.Gpu;

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
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor);
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor, GpuImage depthImage, float clearDepth);
    //CreateRenderPass overload with colorLoadOp; Load preserves previous color content for multi-slot shared atlases
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor, GpuImage depthImage, float clearDepth, GpuLoadOp colorLoadOp);
    //CopyBuffer copies the source buffer to the destination buffer
    void CopyBuffer(GpuBuffer src, GpuBuffer dst, ulong srcOffset, ulong dstOffset, ulong size);
    //WriteToTexture uploads pixel data to an image via staging
    void WriteToTexture(GpuImage dst, ReadOnlySpan<byte> data, int dstX, int dstY, int width, int height);
    //TransitionImageLayout records an image layout transition into the current command buffer, for switching an offscreen render to a sampling layout
    //VulkanImage.TransitionLayout internally checks currentLayout==newLayout and safely skips redundant transitions
    void TransitionImageLayout(GpuImage image, GpuImageLayout newLayout);
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
