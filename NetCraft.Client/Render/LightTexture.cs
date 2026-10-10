using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;
namespace NetCraft.Client.Render;

//LightTexture 16x16 lightmap, maps to vanilla com.mojang.blaze3d.systems.LightTexture
//Horizontal axis blockLight(0-15) vertical axis skyLight(0-15); the pixel value is the combined brightness RGB
//Vanilla updates every frame by GameTime/weather/dimension; the PoC simplifies to a statically generated full-brightness table, enough for GUI items
//The vertex light attribute is a packed int (blockLight<<4)|(skyLight<<20); the shader unpacks and samples this texture
//PackLightCoords combines block/sky into a packed int, maps to vanilla LightTexture.pack
public sealed class LightTexture : IDisposable
{
    public const int Size = 16;
    public const int FullBlockLight = 15;
    public const int FullSkyLight = 15;
    //FullBrightCoords full bright packed (15<<4)|(15<<20)=0x00F000F0, maps to vanilla FULL_BRIGHT
    public const int FullBrightCoords = (FullBlockLight << 4) | (FullSkyLight << 20);

    private readonly GpuDevice? _device;
    private GpuImage? _texture;
    private GpuSampler? _sampler;
    //_lastSkyBrightness the uploaded brightness; CreateResources uploads 1.0 first, consistent with the initial value
    private float _lastSkyBrightness = 1.0f;
    private bool _disposed;

    public LightTexture(GpuDevice? device = null)
    {
        _device = device;
        if (device?.SupportsGpuRendering == true)
            CreateResources();
    }

    //Texture lightmap texture; null means no GPU backend
    public GpuImage? Texture => _texture;
    //Sampler lightmap sampler; null means no GPU backend
    public GpuSampler? Sampler => _sampler;

    //PackLightCoords packs block/sky light levels into an int, maps to vanilla LightTexture.pack
    //blockLight/skyLight 0-15 after packing the low 4 bits are empty, the middle 4 bits are block and the high 4 bits sky
    public static int PackLightCoords(int blockLight, int skyLight)
        => ((blockLight & 0xF) << 4) | ((skyLight & 0xF) << 20);

    //UnpackBlockLight extracts blockLight 0-15 from the packed int
    public static int UnpackBlockLight(int packed) => (packed >> 4) & 0xF;
    //UnpackSkyLight extracts skyLight 0-15 from the packed int
    public static int UnpackSkyLight(int packed) => (packed >> 20) & 0xF;

    //ComputePixel statically computes the RGB brightness 0-255 for the given block/sky light levels
    //Vanilla LightTexture.updateTexture uses a gamma=0.4 correction + skyDarken falloff + dim factor
    //The PoC simplifies to block-dominant with sky auxiliary, taking max and quantizing to 0-255 after gamma correction
    //When blockLight is full brightness 15 it returns 255 directly (full bright)
    public static (byte R, byte G, byte B) ComputePixel(int blockLight, int skyLight, float skyBrightness = 1.0f)
    {
        var blockF = blockLight / 15f;
        var skyF = skyLight / 15f * skyBrightness;
        //Takes the max of the combination, emulating vanilla's maximum light contribution from block+sky
        var combined = MathF.Max(blockF, skyF);
        //Gamma correction 0.4 brightens the shadows to match the vanilla look
        var gamma = MathF.Pow(combined, 0.4f);
        var v = (byte)Math.Clamp(gamma * 255f, 0f, 255f);
        return (v, v, v);
    }

    //GeneratePixels statically generates 16x16 RGBA pixel data shared by CPU tests + GPU Upload
    //Horizontal axis blockLight vertical axis skyLight row-major pixels[(y*16+x)*4] = (R,G,B,A)
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

    //CreateResources creates the GPU texture + sampler, called only when SupportsGpuRendering=true
    private void CreateResources()
    {
        _texture = _device!.CreateImage(new GpuImageDescription
        {
            Width = Size,
            Height = Size,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        //The initial full-brightness table: the GUI item FullBright position (15,15) should be pure white
        _texture.Upload(GeneratePixels(1.0f));
        //The small 16x16 texture uses nearest to avoid adjacent light levels blurring into each other
        _sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
    }

    //Update regenerates pixels for the given skyBrightness and uploads to the GPU; a no-op without a GPU backend
    //Returns immediately when the brightness is unchanged, so per-frame calls do not re-upload
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
