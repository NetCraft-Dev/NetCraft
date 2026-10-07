namespace NetCraft.Gpu;

//GpuImageFormat pixel format
public enum GpuImageFormat
{
    R8G8B8A8Unorm,
    B8G8R8A8Unorm,
    R8G8B8Unorm,
    R8Unorm,
    D32Sfloat
}

//GpuImageUsage image usage, bitwise combinable; a blur offscreen needs ColorAttachment|SampledImage
[Flags]
public enum GpuImageUsage
{
    SampledImage = 1,
    ColorAttachment = 2,
    DepthAttachment = 4
}

//GpuImageDescription image creation description
public sealed class GpuImageDescription
{
    public int Width { get; set; }
    public int Height { get; set; }
    public GpuImageFormat Format { get; set; }
    public GpuImageUsage Usage { get; set; }
    public int MipLevels { get; set; } = 1;
}

//GpuImage GPU image/texture abstraction, corresponds to vanilla blaze3d Texture
//Subclasses provide Upload and the ImageView creation entry point
public abstract class GpuImage : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public GpuImageFormat Format { get; }
    public GpuImageUsage Usage { get; }
    public int MipLevels { get; }

    protected GpuImage(GpuImageDescription desc)
    {
        Width = desc.Width;
        Height = desc.Height;
        Format = desc.Format;
        Usage = desc.Usage;
        MipLevels = desc.MipLevels;
    }

    //Upload uploads pixel data; the byte length must match Width*Height*bytes per pixel
    public abstract void Upload(ReadOnlySpan<byte> pixels);

    //UploadRegion uploads pixels region-wise into an existing atlas texture, corresponds to vanilla GlyphBitmap.upload(x,y,texture)
    //Used by dynamic glyph baking: after the first Upload, writes new glyph pixels into atlas sub-regions on demand
    //The default throws NotSupportedException; backends override as needed
    public virtual void UploadRegion(int x, int y, int width, int height, ReadOnlySpan<byte> pixels)
        => throw new NotSupportedException("UploadRegion is not implemented in this backend");

    //Readback reads GPU image pixels back into a CPU byte array for integration tests to verify render output
    //The returned byte array length = Width*Height*bytes per pixel (R8G8B8A8=4)
    //The default throws NotSupportedException; only the Vulkan backend overrides it, for gpugui tests
    public virtual byte[] Readback()
        => throw new NotSupportedException("Readback is not implemented in this backend");

    public virtual void Dispose() { }
}
