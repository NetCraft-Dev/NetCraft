namespace NetCraft.Gpu;

//ItemTextureAtlas 物品纹理图集对标原版 TextureAtlas
//PoC 程序化生成 16x16 单纹理图集供 3D 物品采样
//完整版应从 textures/texture_atlas.png 加载 + 按 sprite 元数据切分
//顶点 UV 是相对图集的 [0,1] 坐标 shader 直接 texture(sampler, fragUv) 采样
//GenerateTestTexture 生成棋盘格测试纹理验证 UV 采样可见每个格子不同色
public sealed class ItemTextureAtlas : IDisposable
{
    public const int AtlasSize = 16;

    private readonly GpuDevice? _device;
    private GpuImage? _texture;
    private GpuSampler? _sampler;
    private bool _disposed;

    public ItemTextureAtlas(GpuDevice? device = null)
    {
        _device = device;
        if (device?.SupportsGpuRendering == true)
            CreateResources();
    }

    //Texture 物品纹理图集 null 表示无 GPU 后端
    public GpuImage? Texture => _texture;
    //Sampler 物品纹理采样器 null 表示无 GPU 后端
    public GpuSampler? Sampler => _sampler;

    //CreateResources 创建 GPU 纹理 + sampler 仅 SupportsGpuRendering=true 调用
    private void CreateResources()
    {
        _texture = _device!.CreateImage(new GpuImageDescription
        {
            Width = AtlasSize,
            Height = AtlasSize,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        _texture.Upload(GenerateTestTexture());
        //物品纹理小图集用 nearest 保持像素感匹配原版 Minecraft 像素风格
        _sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
    }

    //GenerateTestTexture 生成 16x16 RGBA 棋盘格测试纹理
    //4x4 格子每格 8x8 像素格子颜色交替深浅灰便于验证 UV 采样
    //横轴 2 格纵轴 2 格 (0,0)=深灰 (1,0)=浅灰 (0,1)=浅灰 (1,1)=深灰
    public static byte[] GenerateTestTexture()
    {
        var pixels = new byte[AtlasSize * AtlasSize * 4];
        var cellSize = AtlasSize / 2;
        for (int y = 0; y < AtlasSize; y++)
        {
            for (int x = 0; x < AtlasSize; x++)
            {
                var cellX = x / cellSize;
                var cellY = y / cellSize;
                //棋盘格交替深浅灰
                var isLight = (cellX + cellY) % 2 == 0;
                var v = (byte)(isLight ? 200 : 80);
                var idx = (y * AtlasSize + x) * 4;
                pixels[idx] = v;
                pixels[idx + 1] = v;
                pixels[idx + 2] = v;
                pixels[idx + 3] = 255;
            }
        }
        return pixels;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _texture?.Dispose();
        _sampler?.Dispose();
        _disposed = true;
    }
}

//ItemTints 物品染色表对标原版 net.minecraft.client.color.item.ItemTints
//原版按 tintIndex 查 dyeColor/potionColor/leatherColor PoC 简化为静态白
//tintIndex=-1 表示无 tint 返回白色 0xFFFFFFFF
//PackColor 把 RGBA 4 byte packed 成 int ARGB 对标原版 vertex color 格式
public static class ItemTints
{
    //White 白色 packed ARGB 0xFFFFFFFF = -1 (int) 对标原版默认 tint
    public const int White = unchecked((int)0xFFFFFFFF);

    //PackColor 把 RGBA packed 成 int ARGB 高 8 位 A 依次 BGR
    //对标原版 vertex color 格式 shader 用 (color>>16)&0xFF 取 R 等位运算解包
    public static int PackColor(byte r, byte g, byte b, byte a = 255)
        => (a << 24) | (r << 16) | (g << 8) | b;

    //GetTint 按 tintIndex 返回染色颜色 PoC 全返回白色
    //tintIndex=-1 或未知都返回白色完整版按 tintIndex 查染色表
    public static int GetTint(int tintIndex) => White;
}
