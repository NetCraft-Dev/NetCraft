namespace NetCraft.Client.Blaze3d.Textures;

//GpuSampler GPU texture sampler, maps to vanilla com.mojang.blaze3d.textures.GpuSampler
public abstract class GpuSampler : IDisposable
{
    public abstract AddressMode AddressModeU { get; }
    public abstract AddressMode AddressModeV { get; }
    public abstract FilterMode MinFilter { get; }
    public abstract FilterMode MagFilter { get; }
    public abstract int MaxAnisotropy { get; }
    public abstract double? MaxLod { get; }

    public abstract void Dispose();
}
