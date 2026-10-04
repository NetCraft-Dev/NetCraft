namespace NetCraft.Gpu;

//GpuImageFormat 像素格式
public enum GpuImageFormat
{
    R8G8B8A8Unorm,
    B8G8R8A8Unorm,
    R8G8B8Unorm,
    R8Unorm,
    D32Sfloat
}

//GpuImageUsage 图像用途支持按位组合 blur offscreen 需 ColorAttachment|SampledImage
[Flags]
public enum GpuImageUsage
{
    SampledImage = 1,
    ColorAttachment = 2,
    DepthAttachment = 4
}

//GpuImageDescription 图像创建描述
public sealed class GpuImageDescription
{
    public int Width { get; set; }
    public int Height { get; set; }
    public GpuImageFormat Format { get; set; }
    public GpuImageUsage Usage { get; set; }
    public int MipLevels { get; set; } = 1;
}

//GpuImage GPU 图像/纹理抽象对应原版 blaze3d Texture
//子类提供 Upload 和创建 ImageView 入口
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

    //Upload 上传像素数据字节数据长度需匹配 Width*Height*像素字节数
    public abstract void Upload(ReadOnlySpan<byte> pixels);

    //UploadRegion 按区域上传像素到已存在的图集纹理对应原版 GlyphBitmap.upload(x,y,texture)
    //用于动态字形烘焙首次 Upload 后按需把新字形像素写到图集子区域
    //默认实现抛 NotSupportedException 后端按需 override
    public virtual void UploadRegion(int x, int y, int width, int height, ReadOnlySpan<byte> pixels)
        => throw new NotSupportedException("UploadRegion 未在此后端实现");

    //Readback 把 GPU 图像像素读回 CPU 字节数组供集成测试验证渲染结果
    //返回字节数组长度 = Width*Height*像素字节数(R8G8B8A8=4)
    //默认实现抛 NotSupportedException 仅 Vulkan 后端 override 供 gpugui 测试用
    public virtual byte[] Readback()
        => throw new NotSupportedException("Readback 未在此后端实现");

    public virtual void Dispose() { }
}
