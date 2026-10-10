using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//GpuDeviceBackend the raw device operations, aligns with vanilla com.mojang.blaze3d.systems.GpuDeviceBackend
//GpuDevice wraps this and adds argument validation
public interface GpuDeviceBackend : IDisposable
{
    GpuSurfaceBackend CreateSurface(long windowHandle);

    CommandEncoderBackend CreateCommandEncoder();

    GpuSampler CreateSampler(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, int maxAnisotropy, double? maxLod);

    GpuTexture CreateTexture(string? label, int usage, GpuFormat format, int width, int height, int depthOrLayers, int mipLevels);

    GpuTextureView CreateTextureView(GpuTexture texture);

    GpuTextureView CreateTextureView(GpuTexture texture, int baseMipLevel, int mipLevels);

    GpuBuffer CreateBuffer(string? label, int usage, long size);

    IReadOnlyList<string> GetLastDebugMessages();

    bool IsDebuggingEnabled { get; }

    CompiledRenderPipeline PrecompilePipeline(RenderPipeline pipeline);

    void ClearPipelineCache();

    GpuQueryPool CreateTimestampQueryPool(int size);

    long GetTimestampNow();

    DeviceInfo GetDeviceInfo();
}
