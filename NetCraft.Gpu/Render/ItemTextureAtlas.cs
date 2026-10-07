namespace NetCraft.Gpu;

//ItemTextureAtlas item texture atlas, maps to vanilla TextureAtlas
//The PoC procedurally generates a 16x16 single-texture atlas for 3D item sampling
//The full version should load from textures/texture_atlas.png and split by sprite metadata
//Vertex UVs are [0,1] coordinates relative to the atlas; the shader samples directly with texture(sampler, fragUv)
//GenerateTestTexture generates a checkerboard test texture so UV sampling is visible with a different color per cell
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

    //Texture item texture atlas; null means no GPU backend
    public GpuImage? Texture => _texture;
    //Sampler item texture sampler; null means no GPU backend
    public GpuSampler? Sampler => _sampler;

    //CreateResources creates the GPU texture + sampler, called only when SupportsGpuRendering=true
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
        //The small item texture atlas uses nearest to keep the pixel feel, matching vanilla Minecraft's pixel style
        _sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
    }

    //GenerateTestTexture generates a 16x16 RGBA checkerboard test texture
    //4x4 cells of 8x8 pixels each, alternating light and dark gray to verify UV sampling
    //2 cells horizontally and 2 vertically (0,0)=dark gray (1,0)=light gray (0,1)=light gray (1,1)=dark gray
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
                //Checkerboard alternating light and dark gray
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

//ItemTints item tint table, maps to vanilla net.minecraft.client.color.item.ItemTints
//Vanilla looks up dyeColor/potionColor/leatherColor by tintIndex; the PoC simplifies to static white
//tintIndex=-1 means no tint and returns white 0xFFFFFFFF
//PackColor packs RGBA 4 bytes into an ARGB int, maps to the vanilla vertex color format
public static class ItemTints
{
    //White white packed ARGB 0xFFFFFFFF = -1 (int), maps to the vanilla default tint
    public const int White = unchecked((int)0xFFFFFFFF);

    //PackColor packs RGBA into an ARGB int with the high 8 bits A then BGR
    //maps to the vanilla vertex color format; the shader unpacks with bit ops like (color>>16)&0xFF for R
    public static int PackColor(byte r, byte g, byte b, byte a = 255)
        => (a << 24) | (r << 16) | (g << 8) | b;

    //GetTint returns the tint color by tintIndex; the PoC always returns white
    //tintIndex=-1 or unknown both return white; the full version looks up the tint table by tintIndex
    public static int GetTint(int tintIndex) => White;
}
