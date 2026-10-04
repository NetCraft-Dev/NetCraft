namespace NetCraft.Gpu.Font;

//GlyphStitcher 字形缝合器对标原版 GlyphStitcher
//管理多个 FontTexture 图集按需新建首个装不下时新建下一张
//Stitch 把 IGlyphInfo+IGlyphBitmap 缝到图集返回 BakedGlyph
//colored 字形走 RGBA8 图集 grayscale 字形走 R8 图集两类图集独立分配
//F7 接入 GuiResourceManager 创建 FontTexture 时注册图集获得 TextureSetup + 按 colored 创建 GlyphRenderTypes
public sealed class GlyphStitcher : IUnbakedGlyph.Stitcher, IDisposable
{
    private readonly GpuDevice _device;
    private readonly GuiResourceManager _resourceManager;
    private readonly List<FontTexture> _textures = new();

    public GlyphStitcher(GpuDevice device, GuiResourceManager resourceManager)
    {
        _device = device;
        _resourceManager = resourceManager;
    }

    //Stitch 遍历已有图集找首个能装下的装不下则新建图集
    //对标原版 GlyphStitcher.stitch 遍历 textures 调 add 失败则新建
    public BakedGlyph Stitch(IGlyphInfo info, IGlyphBitmap bitmap)
    {
        foreach (var texture in _textures)
        {
            var glyph = texture.Add(info, bitmap);
            if (glyph != null) return glyph;
        }
        var newTexture = CreateTexture(bitmap.IsColored);
        _textures.Add(newTexture);
        return newTexture.Add(info, bitmap) ?? throw new InvalidOperationException(
            $"字形 {info.Advance} 装不下新建的 256×256 图集 pixelSize={bitmap.PixelWidth}x{bitmap.PixelHeight}");
    }

    //CreateTexture 创建新图集 GpuImage 注册到 GuiResourceManager 获得 TextureSetup + 创建 GlyphRenderTypes
    //colored=true 用 RGBA8 图集 + CreateForColorTexture colored=false 用 R8 图集 + CreateForGrayscaleTexture
    private FontTexture CreateTexture(bool colored)
    {
        var desc = new GpuImageDescription
        {
            Width = FontTexture.Size,
            Height = FontTexture.Size,
            Format = colored ? GpuImageFormat.R8G8B8A8Unorm : GpuImageFormat.R8Unorm,
            Usage = GpuImageUsage.SampledImage,
            MipLevels = 1
        };
        var image = _device.CreateImage(desc);
        var textureSetup = _resourceManager.RegisterFontTexture(image);
        var renderTypes = colored
            ? GlyphRenderTypes.CreateForColorTexture()
            : GlyphRenderTypes.CreateForGrayscaleTexture();
        return new FontTexture(image, colored, textureSetup, renderTypes);
    }

    //GetMissing 返回缺失字形占位对标原版 AllMissingGlyphProvider 返回 SpecialGlyphs.MISSING
    //SpecialGlyphs.Missing 5x8 白色边框 Bake 调 Stitch 缝到 RGBA8 图集返回 SheetBakedGlyph
    //渲染时由调用方 color 着色显示为紫色方块是着色效果
    public BakedGlyph GetMissing() => SpecialGlyphs.Missing.Bake(this);

    //Reset 释放所有图集纹理对标原版 GlyphStitcher.reset
    public void Reset()
    {
        foreach (var texture in _textures)
            texture.Dispose();
        _textures.Clear();
    }

    public void Dispose() => Reset();
}
