namespace NetCraft.Gpu;

//TextureStitcher texture atlas stitcher, maps to the vanilla TextureAtlas stitching stage
//Packs multiple sprites into the smallest atlas texture using a shelf algorithm
//Sprites are sorted by descending height and placed row by row with rows aligned to their height; simple and reliable
//Most block textures are 16x16, so shelf utilization is sufficient
//A pure algorithm independent of GpuDevice; BlockTextureAtlas calls it and uploads the result to a GpuImage
public sealed class TextureStitcher
{
    //Padding pixel gap between sprites to prevent linear-sampling bleeding
    private const int Padding = 1;

    private readonly int _maxAtlasSize;

    //AtlasWidth/AtlasHeight the stitched atlas size
    public int AtlasWidth { get; private set; }
    public int AtlasHeight { get; private set; }

    //_placed list of placed sprites in input order
    private readonly List<PlacedSprite> _placed = new();
    public IReadOnlyList<PlacedSprite> Placed => _placed;

    public TextureStitcher(int maxAtlasSize = 1024)
    {
        _maxAtlasSize = maxAtlasSize;
    }

    //Stitch stitches all sprites and returns whether it succeeded
    //Sorts by descending height so large sprites go first, improving shelf utilization
    //The atlas size doubles from 16 until all sprites fit or maxAtlasSize is reached
    public bool Stitch(IReadOnlyList<SpriteInput> sprites)
    {
        if (sprites.Count == 0)
        {
            AtlasWidth = 16;
            AtlasHeight = 16;
            return true;
        }
        var sorted = sprites.OrderByDescending(s => Math.Max(s.Width, s.Height)).ThenBy(s => s.Name).ToList();
        //Tries a square power of two first
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
        //If the square does not fit, tries a 2:1 wide rectangle
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

    //TryPack shelf-algorithm packing
    //currentShelfY current row bottom y currentShelfH current row height currentX current used row width
    //If a sprite does not fit the current row, start a new row with height=the sprite's height
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
            //Does not fit the current row, start a new one
            if (shelfX + w > atlasWidth)
            {
                shelfY += shelfH + Padding;
                shelfX = 0;
                shelfH = 0;
            }
            //Height exceeds the atlas, does not fit
            if (shelfY + h > atlasHeight) return false;
            //Row height takes the tallest sprite height
            if (h > shelfH) shelfH = h;
            sprite._atlasX = shelfX;
            sprite._atlasY = shelfY;
            shelfX += w + Padding;
        }
        return true;
    }

    //BuildPlaced builds the PlacedSprite list in original input order
    private void BuildPlaced(IReadOnlyList<SpriteInput> sprites)
    {
        _placed.Clear();
        foreach (var s in sprites)
            _placed.Add(new PlacedSprite(s.Name, s._atlasX, s._atlasY, s.Width, s.Height, s.Pixels));
    }

    //SpriteInput stitching input sprite
    //Name identifier minecraft:block/stone
    //Width/Height pixel size
    //Pixels RGBA data; null means placeholder
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

    //PlacedSprite stitching result including the atlas position
    public sealed record PlacedSprite(string Name, int AtlasX, int AtlasY, int Width, int Height, byte[]? Pixels);
}
