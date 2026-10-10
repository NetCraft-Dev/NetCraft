using System.Numerics;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//CommandEncoderBackend raw command recording operations, aligns with vanilla CommandEncoderBackend
//NetCraft makes it disposable because every encoder owns its own command buffer and fence
public interface CommandEncoderBackend : IDisposable
{
    void Submit();

    TransientMemory TransientMemory();

    RenderPassBackend CreateRenderPass(RenderPassDescriptor descriptor);

    void SubmitRenderPass();

    void ClearColorTexture(GpuTexture texture, Vector4 color);

    void ClearColorAndDepthTextures(GpuTexture colorTexture, Vector4 color, GpuTexture depthTexture, double depth);

    void ClearColorAndDepthTextures(GpuTexture colorTexture, Vector4 color, GpuTexture depthTexture, double depth, int regionX, int regionY, int regionWidth, int regionHeight);

    void ClearDepthTexture(GpuTexture depthTexture, double depth);

    void WriteToBuffer(GpuBufferSlice destination, ReadOnlySpan<byte> data);

    void CopyToBuffer(GpuBufferSlice source, GpuBufferSlice target);

    void WriteToTexture(GpuTexture destination, ReadOnlySpan<byte> data, int mipLevel, int depthOrLayer, int destX, int destY, int width, int height);

    void CopyBufferToTexture(GpuBufferSlice source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, GpuTexture destination, int destinationX, int destinationY, int copyWidth, int copyHeight, int mipLevel, int arrayLayer);

    void CopyTextureToBuffer(GpuTexture source, GpuBuffer destination, long offset, System.Action? callback, int mipLevel);

    void CopyTextureToBuffer(GpuTexture source, GpuBuffer destination, long offset, System.Action? callback, int mipLevel, int x, int y, int width, int height);

    void CopyTextureToTexture(GpuTexture source, GpuTexture destination, int mipLevel, int destX, int destY, int sourceX, int sourceY, int width, int height);

    GpuFence CreateFence();

    void WriteTimestamp(GpuQueryPool pool, int index);
}
