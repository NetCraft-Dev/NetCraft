using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//GpuDevice the logical GPU device, aligns with vanilla com.mojang.blaze3d.systems.GpuDevice
//A thin validating wrapper over GpuDeviceBackend; the backend owns the native handles
public sealed class GpuDevice : IDisposable
{
    private readonly GpuDeviceBackend _backend;
    private readonly System.Action _criticalShaderLoader;

    public GpuDevice(GpuDeviceBackend backend, System.Action criticalShaderLoader)
    {
        _backend = backend;
        _criticalShaderLoader = criticalShaderLoader;
    }

    //Limits convenience accessor for the device limits
    public DeviceLimits Limits => GetDeviceInfo().Limits;

    public GpuSurface CreateSurface(long windowHandle) => new(_backend.CreateSurface(windowHandle));

    public CommandEncoder CreateCommandEncoder() => new(_backend, _backend.CreateCommandEncoder());

    public GpuSampler CreateSampler(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, int maxAnisotropy, double? maxLod)
    {
        int maxSupportedAnisotropy = GetDeviceInfo().Limits.MaxAnisotropy;
        if (maxAnisotropy < 1 || maxAnisotropy > maxSupportedAnisotropy)
            throw new ArgumentException($"maxAnisotropy out of range; must be >= 1 and <= {maxSupportedAnisotropy}, but was {maxAnisotropy}");
        return _backend.CreateSampler(addressModeU, addressModeV, minFilter, magFilter, maxAnisotropy, maxLod);
    }

    public GpuTexture CreateTexture(string? label, int usage, GpuFormat format, int width, int height, int depthOrLayers, int mipLevels)
    {
        VerifyTextureCreationArgs(usage, width, height, depthOrLayers, mipLevels);
        return _backend.CreateTexture(label, usage, format, width, height, depthOrLayers, mipLevels);
    }

    public GpuTextureView CreateTextureView(GpuTexture texture)
    {
        VerifyTextureViewCreationArgs(texture, 0, texture.MipLevels);
        return _backend.CreateTextureView(texture, 0, texture.MipLevels);
    }

    public GpuTextureView CreateTextureView(GpuTexture texture, int baseMipLevel, int mipLevels)
    {
        VerifyTextureViewCreationArgs(texture, baseMipLevel, mipLevels);
        return _backend.CreateTextureView(texture, baseMipLevel, mipLevels);
    }

    public GpuBuffer CreateBuffer(string? label, int usage, long size)
    {
        if (size <= 0)
            throw new ArgumentException("Buffer size must be greater than zero");
        return _backend.CreateBuffer(label, usage, size);
    }

    public IReadOnlyList<string> GetLastDebugMessages() => _backend.GetLastDebugMessages();

    public bool IsDebuggingEnabled => _backend.IsDebuggingEnabled;

    public CompiledRenderPipeline PrecompilePipeline(RenderPipeline pipeline) => _backend.PrecompilePipeline(pipeline);

    public void ClearPipelineCache() => _backend.ClearPipelineCache();

    public void LoadCriticalShaders() => _criticalShaderLoader();

    public GpuQueryPool CreateTimestampQueryPool(int size) => _backend.CreateTimestampQueryPool(size);

    public long GetTimestampNow() => _backend.GetTimestampNow();

    public DeviceInfo GetDeviceInfo() => _backend.GetDeviceInfo();

    public void Dispose() => _backend.Dispose();

    private static void VerifyTextureCreationArgs(int usage, int width, int height, int depthOrLayers, int mipLevels)
    {
        if (mipLevels < 1)
            throw new ArgumentException("mipLevels must be at least 1");
        int maxDimension = Math.Max(width, height);
        int maxMipSupported = Log2(maxDimension) + 1;
        if (mipLevels > maxMipSupported)
            throw new ArgumentException($"mipLevels must be at most {maxMipSupported} for a texture of width {width} and height {height} (asked for {mipLevels} mipLevels)");
        if (depthOrLayers < 1)
            throw new ArgumentException("depthOrLayers must be at least 1");
        bool isCubemap = (usage & GpuTexture.UsageCubemapCompatible) != 0;
        if (isCubemap)
        {
            if (width != height)
                throw new ArgumentException($"Cubemap compatible textures must be square, but size is {width}x{height}");
            if (depthOrLayers % 6 != 0)
                throw new ArgumentException($"Cubemap compatible textures must have a layer count with a multiple of 6, was {depthOrLayers}");
            if (depthOrLayers > 6)
                throw new NotSupportedException("Array textures are not yet supported");
        }
        else if (depthOrLayers > 1)
        {
            throw new NotSupportedException("Array or 3D textures are not yet supported");
        }
    }

    private static void VerifyTextureViewCreationArgs(GpuTexture texture, int baseMipLevel, int mipLevels)
    {
        if (texture.IsClosed)
            throw new ArgumentException("Can't create texture view with closed texture");
        if (baseMipLevel < 0 || baseMipLevel + mipLevels > texture.MipLevels)
            throw new ArgumentException($"{mipLevels} mip levels starting from {baseMipLevel} would be out of range for texture with only {texture.MipLevels} mip levels");
    }

    //Log2 base-2 logarithm of a positive integer, maps to vanilla Mth.log2
    private static int Log2(int value)
    {
        int result = 0;
        while (value > 1)
        {
            value >>= 1;
            result++;
        }
        return result;
    }
}
