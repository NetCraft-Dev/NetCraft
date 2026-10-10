using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//SamplerCache prebuilt sampler combinations, aligns with vanilla com.mojang.blaze3d.systems.SamplerCache
//Vanilla pulls the device from RenderSystem; NetCraft takes it through the constructor
public sealed class SamplerCache : IDisposable
{
    private readonly GpuSampler[] _samplers = new GpuSampler[32];
    private readonly GpuDevice _device;

    public SamplerCache(GpuDevice device)
    {
        _device = device;
        Initialize();
    }

    private void Initialize()
    {
        foreach (var addressModeU in Enum.GetValues<AddressMode>())
            foreach (var addressModeV in Enum.GetValues<AddressMode>())
                foreach (var minFilter in Enum.GetValues<FilterMode>())
                    foreach (var magFilter in Enum.GetValues<FilterMode>())
                        foreach (var useMipmaps in new[] { true, false })
                            _samplers[Encode(addressModeU, addressModeV, minFilter, magFilter, useMipmaps)] =
                                _device.CreateSampler(addressModeU, addressModeV, minFilter, magFilter, 1, useMipmaps ? null : 0.0);
    }

    public GpuSampler GetSampler(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, bool useMipmaps)
        => _samplers[Encode(addressModeU, addressModeV, minFilter, magFilter, useMipmaps)];

    public GpuSampler GetClampToEdge(FilterMode minMag) => GetSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, minMag, minMag, false);

    public GpuSampler GetClampToEdge(FilterMode minMag, bool mipmaps) => GetSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, minMag, minMag, mipmaps);

    public GpuSampler GetRepeat(FilterMode minMag) => GetSampler(AddressMode.Repeat, AddressMode.Repeat, minMag, minMag, false);

    public GpuSampler GetRepeat(FilterMode minMag, bool mipmaps) => GetSampler(AddressMode.Repeat, AddressMode.Repeat, minMag, minMag, mipmaps);

    //Encode packs a sampler combination into an index, maps to vanilla SamplerCache.encode
    internal static int Encode(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, bool useMipmaps)
    {
        var result = 0;
        result |= (int)addressModeU & 1;
        result |= ((int)addressModeV & 1) << 1;
        result |= ((int)minFilter & 1) << 2;
        result |= ((int)magFilter & 1) << 3;
        if (useMipmaps)
            result |= 0x10;
        return result;
    }

    public void Dispose()
    {
        foreach (var sampler in _samplers)
            sampler?.Dispose();
    }
}
