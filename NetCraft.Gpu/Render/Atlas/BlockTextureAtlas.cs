namespace NetCraft.Gpu;

//ITextureAtlas texture atlas query interface for BlockModelBaker to decouple from GpuDevice
//BlockTextureAtlas implements it for production; tests can use a stub
public interface ITextureAtlas
{
    //GetSprite looks up a sprite by name, returns null if not found
    TextureAtlasSprite? GetSprite(string name);
}

//BlockTextureAtlas block texture atlas, maps to vanilla TextureAtlas
//Takes a sprite list (name+pixels), stitches it into a GpuImage with TextureStitcher and provides name→TextureAtlasSprite lookup
//The GPU layer only does stitching+upload+UV lookup and does not scan assets (scanning is done by the Game layer's BlockTextureCollector)
//nearest sampler keeps the pixel style from blurring block texture edges
//Animated frames are unsupported and to be added later
public sealed class BlockTextureAtlas : ITextureAtlas, IDisposable
{
    private readonly GpuDevice _device;
    private readonly int _maxAtlasSize;
    private GpuImage? _atlasImage;
    private GpuSampler? _sampler;
    private readonly Dictionary<string, TextureAtlasSprite> _sprites = new();

    //AtlasImage the stitched and uploaded atlas texture; null means not yet Built
    public GpuImage? AtlasImage => _atlasImage;
    //Sampler atlas sampler with nearest filtering
    public GpuSampler? Sampler => _sampler;
    //Width/Height atlas size
    public int Width { get; private set; }
    public int Height { get; private set; }

    public BlockTextureAtlas(GpuDevice device, int maxAtlasSize = 1024)
    {
        _device = device;
        _maxAtlasSize = maxAtlasSize;
    }

    //Build stitches the sprite list and uploads to a GpuImage
    //Calling it again Disposes the old atlas and rebuilds
    //Returns false when it does not fit within maxAtlasSize
    public bool Build(IReadOnlyList<TextureStitcher.SpriteInput> sprites)
    {
        DisposeAtlas();
        var stitcher = new TextureStitcher(_maxAtlasSize);
        if (!stitcher.Stitch(sprites))
            return false;
        Width = stitcher.AtlasWidth;
        Height = stitcher.AtlasHeight;
        //Creates the atlas texture; ColorAttachment is not needed, only SampledImage
        _atlasImage = _device.CreateImage(new GpuImageDescription
        {
            Width = Width,
            Height = Height,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        //Nearest sampling preserves the pixel style and does not repeat addresses to avoid bleeding
        _sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
        //Clears everything first, then uploads each sprite region-wise
        //Clearing avoids garbage data in uncovered regions
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

    //GetSprite looks up a sprite by name, returns null if not found
    //name format minecraft:block/stone
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
