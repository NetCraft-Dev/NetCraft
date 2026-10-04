namespace NetCraft.Gpu;

//ITextureAtlas 纹理图集查询接口供 BlockModelBaker 解耦 GpuDevice
//BlockTextureAtlas 实现此接口生产环境用测试可用 stub
public interface ITextureAtlas
{
    //GetSprite 按 name 查询 sprite 未找到返回 null
    TextureAtlasSprite? GetSprite(string name);
}

//BlockTextureAtlas 方块纹理图集对标原版 TextureAtlas
//接收 sprite 列表（name+pixels）用 TextureStitcher 拼接到 GpuImage 提供 name→TextureAtlasSprite 查询
//Gpu 层只做拼接+上传+UV 查询不扫描 assets（扫描由 Game 层 BlockTextureCollector 做）
//nearest sampler 像素风格不模糊方块纹理边
//不支持动画帧后续补
public sealed class BlockTextureAtlas : ITextureAtlas, IDisposable
{
    private readonly GpuDevice _device;
    private readonly int _maxAtlasSize;
    private GpuImage? _atlasImage;
    private GpuSampler? _sampler;
    private readonly Dictionary<string, TextureAtlasSprite> _sprites = new();

    //AtlasImage 拼接上传后的图集纹理 null 表示未 Build
    public GpuImage? AtlasImage => _atlasImage;
    //Sampler 图集采样器 nearest 过滤
    public GpuSampler? Sampler => _sampler;
    //Width/Height 图集尺寸
    public int Width { get; private set; }
    public int Height { get; private set; }

    public BlockTextureAtlas(GpuDevice device, int maxAtlasSize = 1024)
    {
        _device = device;
        _maxAtlasSize = maxAtlasSize;
    }

    //Build 拼接 sprite 列表上传到 GpuImage
    //重复调 Dispose 旧图集重建
    //返回 false 表示 maxAtlasSize 装不下
    public bool Build(IReadOnlyList<TextureStitcher.SpriteInput> sprites)
    {
        DisposeAtlas();
        var stitcher = new TextureStitcher(_maxAtlasSize);
        if (!stitcher.Stitch(sprites))
            return false;
        Width = stitcher.AtlasWidth;
        Height = stitcher.AtlasHeight;
        //创建图集纹理 ColorAttachment 不需要只需 SampledImage
        _atlasImage = _device.CreateImage(new GpuImageDescription
        {
            Width = Width,
            Height = Height,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        //nearest 采样保留像素风格不重复地址避免 bleeding
        _sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
        //先全清零再按区域上传每个 sprite
        //全清零避免未覆盖区域是垃圾数据
        var zero = new byte[Width * Height * 4];
        _atlasImage.Upload(zero);
        foreach (var placed in stitcher.Placed)
        {
            if (placed.Pixels is null) continue;
            _atlasImage.UploadRegion(placed.AtlasX, placed.AtlasY, placed.Width, placed.Height, placed.Pixels);
            var sprite = new TextureAtlasSprite(placed.Name, placed.AtlasX, placed.AtlasY,
                placed.Width, placed.Height, Width, Height, placed.Pixels);
            _sprites[placed.Name] = sprite;
        }
        return true;
    }

    //GetSprite 按 name 查询 sprite 未找到返回 null
    //name 格式 minecraft:block/stone
    public TextureAtlasSprite? GetSprite(string name)
        => _sprites.TryGetValue(name, out var s) ? s : null;

    private void DisposeAtlas()
    {
        _atlasImage?.Dispose();
        _sampler?.Dispose();
        _atlasImage = null;
        _sampler = null;
        _sprites.Clear();
    }

    public void Dispose()
    {
        DisposeAtlas();
        GC.SuppressFinalize(this);
    }
}
