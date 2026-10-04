namespace NetCraft.Gpu;

//LightTexture 16x16 光照贴图对标原版 com.mojang.blaze3d.systems.LightTexture
//横轴 blockLight(0-15) 纵轴 skyLight(0-15) 像素值是组合亮度 RGB
//原版每帧按 GameTime/天气/维度 update PoC 简化静态生成满亮度表 GUI 物品够用
//顶点 light 属性是 packed int (blockLight<<4)|(skyLight<<20) shader 解包采样此贴图
//PackLightCoords 把 block/sky 组合成 packed int 对标原版 LightTexture.pack
public sealed class LightTexture : IDisposable
{
    public const int Size = 16;
    public const int FullBlockLight = 15;
    public const int FullSkyLight = 15;
    //FullBrightCoords 全亮 packed (15<<4)|(15<<20)=0x00F000F0 对标原版 FULL_BRIGHT
    public const int FullBrightCoords = (FullBlockLight << 4) | (FullSkyLight << 20);

    private readonly GpuDevice? _device;
    private GpuImage? _texture;
    private GpuSampler? _sampler;
    //_lastSkyBrightness 已上传的亮度 CreateResources 首次按 1.0 上传 与初值一致
    private float _lastSkyBrightness = 1.0f;
    private bool _disposed;

    public LightTexture(GpuDevice? device = null)
    {
        _device = device;
        if (device?.SupportsGpuRendering == true)
            CreateResources();
    }

    //Texture 光照贴图纹理 null 表示无 GPU 后端
    public GpuImage? Texture => _texture;
    //Sampler 光照贴图采样器 null 表示无 GPU 后端
    public GpuSampler? Sampler => _sampler;

    //PackLightCoords 把 block/sky 光等级 packed 成 int 对标原版 LightTexture.pack
    //blockLight/skyLight 0-15 packed 后低 4 位空 中 4 位 block 高 4 位 sky
    public static int PackLightCoords(int blockLight, int skyLight)
        => ((blockLight & 0xF) << 4) | ((skyLight & 0xF) << 20);

    //UnpackBlockLight 从 packed int 取 blockLight 0-15
    public static int UnpackBlockLight(int packed) => (packed >> 4) & 0xF;
    //UnpackSkyLight 从 packed int 取 skyLight 0-15
    public static int UnpackSkyLight(int packed) => (packed >> 20) & 0xF;

    //ComputePixel 静态计算指定 block/sky 光等级的 RGB 亮度 0-255
    //原版 LightTexture.updateTexture 用 gamma=0.4 校正 + skyDarken 衰减 + dim 因子
    //PoC 简化 block 主导 sky 辅助 取 max 经 gamma 校正后量化到 0-255
    //blockLight 满亮度 15 时直接返回 255 (满亮)
    public static (byte R, byte G, byte B) ComputePixel(int blockLight, int skyLight, float skyBrightness = 1.0f)
    {
        var blockF = blockLight / 15f;
        var skyF = skyLight / 15f * skyBrightness;
        //组合取 max 模拟原版 block+sky 取最大光贡献
        var combined = MathF.Max(blockF, skyF);
        //gamma 校正 0.4 提亮暗部匹配原版视觉
        var gamma = MathF.Pow(combined, 0.4f);
        var v = (byte)Math.Clamp(gamma * 255f, 0f, 255f);
        return (v, v, v);
    }

    //GeneratePixels 静态生成 16x16 RGBA 像素数据供 CPU 测试 + GPU Upload 共用
    //横轴 blockLight 纵轴 skyLight 行优先 pixels[(y*16+x)*4] = (R,G,B,A)
    public static byte[] GeneratePixels(float skyBrightness = 1.0f)
    {
        var pixels = new byte[Size * Size * 4];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var (r, g, b) = ComputePixel(x, y, skyBrightness);
                var idx = (y * Size + x) * 4;
                pixels[idx] = r;
                pixels[idx + 1] = g;
                pixels[idx + 2] = b;
                pixels[idx + 3] = 255;
            }
        }
        return pixels;
    }

    //CreateResources 创建 GPU 纹理 + sampler 仅 SupportsGpuRendering=true 调用
    private void CreateResources()
    {
        _texture = _device!.CreateImage(new GpuImageDescription
        {
            Width = Size,
            Height = Size,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        //初始满亮度表 GUI 物品 FullBright 位置 (15,15) 应纯白
        _texture.Upload(GeneratePixels(1.0f));
        //16x16 小纹理用 nearest 避免相邻光等级模糊串色
        _sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
    }

    //Update 按指定 skyBrightness 重生成像素并上传 GPU 无 GPU 后端时空操作
    //亮度未变化直接返回 调用方每帧调用不会重复上传
    public void Update(float skyBrightness)
    {
        if (_texture is null) return;
        if (MathF.Abs(skyBrightness - _lastSkyBrightness) < 0.001f) return;
        _lastSkyBrightness = skyBrightness;
        _texture.Upload(GeneratePixels(skyBrightness));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _texture?.Dispose();
        _sampler?.Dispose();
        _disposed = true;
    }
}
