using NetCraft.Client.Blaze3d;
namespace NetCraft.Client.Blaze3d.Textures;

//GpuTexture GPU texture, maps to vanilla com.mojang.blaze3d.textures.GpuTexture
//Holds the format and dimensions; the backend subclass owns the native handle
public abstract class GpuTexture : IDisposable
{
    public const int UsageCopyDst = 1;
    public const int UsageCopySrc = 2;
    public const int UsageTextureBinding = 4;
    public const int UsageRenderAttachment = 8;
    public const int UsageCubemapCompatible = 16;

    private readonly int _width;
    private readonly int _height;

    public GpuFormat Format { get; }
    public int DepthOrLayers { get; }
    public int MipLevels { get; }
    public int Usage { get; }
    public string Label { get; }

    protected GpuTexture(int usage, string label, GpuFormat format, int width, int height, int depthOrLayers, int mipLevels)
    {
        Usage = usage;
        Label = label;
        Format = format;
        _width = width;
        _height = height;
        DepthOrLayers = depthOrLayers;
        MipLevels = mipLevels;
    }

    //GetWidth width at the given mip level, maps to vanilla getWidth
    public int GetWidth(int mipLevel) => _width >> mipLevel;

    //GetHeight height at the given mip level, maps to vanilla getHeight
    public int GetHeight(int mipLevel) => _height >> mipLevel;

    //Width width at mip 0, convenience used by the NetCraft renderer
    public int Width => _width;

    //Height height at mip 0, convenience used by the NetCraft renderer
    public int Height => _height;

    //IsClosed whether the texture has been closed, maps to vanilla isClosed
    public abstract bool IsClosed { get; }

    //Upload uploads pixel data, a NetCraft extension; vanilla uploads through CommandEncoder.writeToTexture
    public abstract void Upload(ReadOnlySpan<byte> pixels);

    //UploadRegion uploads a pixel region into an existing atlas texture, maps to vanilla GlyphBitmap.upload(x,y,texture)
    public virtual void UploadRegion(int x, int y, int width, int height, ReadOnlySpan<byte> pixels)
        => throw new NotSupportedException("UploadRegion is not implemented in this backend");

    //Readback reads pixels back to CPU memory, a NetCraft test helper; vanilla reads back through CommandEncoder.copyTextureToBuffer
    public virtual byte[] Readback()
        => throw new NotSupportedException("Readback is not implemented in this backend");

    public abstract void Dispose();
}
