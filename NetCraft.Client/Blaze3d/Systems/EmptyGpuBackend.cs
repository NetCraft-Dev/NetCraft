using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Render;

namespace NetCraft.Client.Blaze3d.Systems;

//EmptyGpuBackend null graphics backend, used where no GPU is available
//Everything still compiles and runs; device creation succeeds and every resource call throws
public sealed class EmptyGpuBackend : GpuBackend
{
    public string Name => "Empty";

    public void SetWindowHints() { }

    public GpuDevice CreateDevice(long windowHandle, ShaderManager shaderManager, GpuDebugOptions debugOptions, System.Action criticalShaderLoader)
        => new GpuDevice(new EmptyGpuDeviceBackend(), shaderManager, criticalShaderLoader);
}

//EmptyGpuDeviceBackend GpuDeviceBackend placeholder for the null backend
internal sealed class EmptyGpuDeviceBackend : GpuDeviceBackend
{
    public GpuSurfaceBackend CreateSurface(long windowHandle)
        => throw new NotSupportedException("Empty backend does not support creating a Surface");

    public CommandEncoderBackend CreateCommandEncoder()
        => throw new NotSupportedException("Empty backend does not support creating a CommandEncoder");

    public GpuSampler CreateSampler(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, int maxAnisotropy, double? maxLod)
        => throw new NotSupportedException("Empty backend does not support creating a Sampler");

    public GpuTexture CreateTexture(string? label, int usage, GpuFormat format, int width, int height, int depthOrLayers, int mipLevels)
        => throw new NotSupportedException("Empty backend does not support creating a Texture");

    public GpuTextureView CreateTextureView(GpuTexture texture)
        => throw new NotSupportedException("Empty backend does not support creating a TextureView");

    public GpuTextureView CreateTextureView(GpuTexture texture, int baseMipLevel, int mipLevels)
        => throw new NotSupportedException("Empty backend does not support creating a TextureView");

    public GpuBuffer CreateBuffer(string? label, int usage, long size)
        => throw new NotSupportedException("Empty backend does not support creating a Buffer");

    public IReadOnlyList<string> GetLastDebugMessages() => Array.Empty<string>();

    public bool IsDebuggingEnabled => false;

    public CompiledRenderPipeline PrecompilePipeline(RenderPipeline pipeline)
        => throw new NotSupportedException("Empty backend does not support compiling a pipeline");

    public void ClearPipelineCache() { }

    public GpuQueryPool CreateTimestampQueryPool(int size)
        => throw new NotSupportedException("Empty backend does not support timestamp queries");

    public long GetTimestampNow() => 0;

    public DeviceInfo GetDeviceInfo() => new(
        "Empty",
        "NetCraft",
        "none",
        true,
        "Empty",
        1f,
        //Placeholder limits so code that reads them still runs without a GPU
        new DeviceLimits(1, 1, 4096, 1L << 40, 1, 8),
        new DeviceFeatures(false, false, false, false, false, false, false),
        new HashSet<string>(),
        new HintsAndWorkarounds(false, false),
        DeviceType.Other);

    public void Dispose() { }
}
