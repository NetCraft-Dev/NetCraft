namespace NetCraft.Gpu;

//TextureStitcher 纹理图集拼接器对标原版 TextureAtlas stitching 阶段
//把多个 sprite 打包到最小图集纹理用 shelf 算法
//sprite 按高度降序排列逐行放置同行高度对齐简单可靠
//方块纹理大多 16x16 shelf 利用率足够
//纯算法不依赖 GpuDevice 由 BlockTextureAtlas 调用后拿结果上传 GpuImage
public sealed class TextureStitcher
{
    //Padding sprite 之间的像素间隔防止线性采样 bleeding
    private const int Padding = 1;

    private readonly int _maxAtlasSize;

    //AtlasWidth/AtlasHeight 拼接后的图集尺寸
    public int AtlasWidth { get; private set; }
    public int AtlasHeight { get; private set; }

    //_placed 已放置 sprite 列表按输入顺序
    private readonly List<PlacedSprite> _placed = new();
    public IReadOnlyList<PlacedSprite> Placed => _placed;

    public TextureStitcher(int maxAtlasSize = 1024)
    {
        _maxAtlasSize = maxAtlasSize;
    }

    //Stitch 拼接所有 sprite 返回是否成功
    //按高度降序排列大 sprite 先放提高 shelf 利用率
    //图集尺寸从 16 开始倍增直到装下所有 sprite 或达到 maxAtlasSize
    public bool Stitch(IReadOnlyList<SpriteInput> sprites)
    {
        if (sprites.Count == 0)
        {
            AtlasWidth = 16;
            AtlasHeight = 16;
            return true;
        }
        var sorted = sprites.OrderByDescending(s => Math.Max(s.Width, s.Height)).ThenBy(s => s.Name).ToList();
        //先尝试正方形 2 的幂
        for (var size = 16; size <= _maxAtlasSize; size *= 2)
        {
            if (TryPack(sorted, size, size))
            {
                AtlasWidth = size;
                AtlasHeight = size;
                BuildPlaced(sprites);
                return true;
            }
        }
        //正方形装不下尝试 2:1 宽矩形
        for (var h = 16; h <= _maxAtlasSize; h *= 2)
        {
            var w = Math.Min(h * 2, _maxAtlasSize);
            if (TryPack(sorted, w, h))
            {
                AtlasWidth = w;
                AtlasHeight = h;
                BuildPlaced(sprites);
                return true;
            }
        }
        return false;
    }

    //TryPack shelf 算法打包
    //currentShelfY 当前行底部 y currentShelfH 当前行高度 currentX 当前行已用宽度
    //sprite 放不下当前行就换行新行高度=当前 sprite 高度
    private bool TryPack(List<SpriteInput> sorted, int atlasWidth, int atlasHeight)
    {
        var shelfY = 0;
        var shelfH = 0;
        var shelfX = 0;
        foreach (var sprite in sorted)
        {
            var w = sprite.Width;
            var h = sprite.Height;
            if (w > atlasWidth || h > atlasHeight) return false;
            //当前行放不下换行
            if (shelfX + w > atlasWidth)
            {
                shelfY += shelfH + Padding;
                shelfX = 0;
                shelfH = 0;
            }
            //高度超出图集放不下
            if (shelfY + h > atlasHeight) return false;
            //行高度取最大 sprite 高度
            if (h > shelfH) shelfH = h;
            sprite._atlasX = shelfX;
            sprite._atlasY = shelfY;
            shelfX += w + Padding;
        }
        return true;
    }

    //BuildPlaced 按原始输入顺序构造 PlacedSprite 列表
    private void BuildPlaced(IReadOnlyList<SpriteInput> sprites)
    {
        _placed.Clear();
        foreach (var s in sprites)
            _placed.Add(new PlacedSprite(s.Name, s._atlasX, s._atlasY, s.Width, s.Height, s.Pixels));
    }

    //SpriteInput 拼接输入 sprite
    //Name 标识符 minecraft:block/stone
    //Width/Height 像素尺寸
    //Pixels RGBA 数据 null 表示占位
    public sealed class SpriteInput
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public byte[]? Pixels { get; }
        internal int _atlasX;
        internal int _atlasY;
        public SpriteInput(string name, int width, int height, byte[]? pixels)
        {
            Name = name;
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }

    //PlacedSprite 拼接结果含图集位置
    public sealed record PlacedSprite(string Name, int AtlasX, int AtlasY, int Width, int Height, byte[]? Pixels);
}
