namespace NetCraft.Client.Blaze3d.Textures;

//GpuTextureView view over a GpuTexture for a mip range, maps to vanilla com.mojang.blaze3d.textures.GpuTextureView
public abstract class GpuTextureView : IDisposable
{
    public GpuTexture Texture { get; }
    public int BaseMipLevel { get; }
    public int MipLevels { get; }

    protected GpuTextureView(GpuTexture texture, int baseMipLevel, int mipLevels)
    {
        Texture = texture;
        BaseMipLevel = baseMipLevel;
        MipLevels = mipLevels;
    }

    //GetWidth width at the given mip level, relative to the view base level, maps to vanilla getWidth
    public int GetWidth(int mipLevel) => Texture.GetWidth(mipLevel + BaseMipLevel);

    //GetHeight height at the given mip level, relative to the view base level, maps to vanilla getHeight
    public int GetHeight(int mipLevel) => Texture.GetHeight(mipLevel + BaseMipLevel);

    //IsClosed whether the view has been closed, maps to vanilla isClosed
    public abstract bool IsClosed { get; }

    public abstract void Dispose();
}
