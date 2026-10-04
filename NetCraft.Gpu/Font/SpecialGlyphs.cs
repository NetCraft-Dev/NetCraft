namespace NetCraft.Gpu.Font;

//SpecialGlyphs 程序生成的特殊字形对标原版 net.minecraft.client.gui.font.glyphs.SpecialGlyphs
//White 5x8 全白 Missing 5x8 白色边框用于缺失 codepoint 占位
//像素在静态构造时生成 advance=width+1 对标原版 getAdvance 返回 image.width+1
//渲染时由调用方 color 着色游戏里显示为紫色方块是着色效果非像素本身紫色
public static class SpecialGlyphs
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 8;

    //Missing 缺失字形占位边框不透明内部透明对标原版 SpecialGlyphs.MISSING
    //edge? -1:0 即边框 0xFFFFFFFF 内部 0x00000000
    public static IUnbakedGlyph Missing { get; } = new SpecialUnbakedGlyph(BuildBitmap(IsEdge));

    //White 全白填充对标原版 SpecialGlyphs.WHITE 所有像素 0xFFFFFFFF
    public static IUnbakedGlyph White { get; } = new SpecialUnbakedGlyph(BuildBitmap((x, y) => true));

    //IsEdge 判断像素是否在边框上对标原版 edge = x==0 || x+1==width || y==0 || y+1==height
    private static bool IsEdge(int x, int y)
        => x == 0 || x + 1 == GlyphWidth || y == 0 || y + 1 == GlyphHeight;

    //BuildBitmap 程序生成 RGBA8 像素数组 edge 像素全 0xFF 否则全 0x00
    //长度=width*height*4 对标原版 NativeImage RGBA 格式 setPixel
    private static SpecialGlyphBitmap BuildBitmap(Func<int, int, bool> pixelProvider)
    {
        var pixels = new byte[GlyphWidth * GlyphHeight * 4];
        for (int y = 0; y < GlyphHeight; y++)
        {
            for (int x = 0; x < GlyphWidth; x++)
            {
                int idx = (y * GlyphWidth + x) * 4;
                byte v = (byte)(pixelProvider(x, y) ? 0xFF : 0x00);
                pixels[idx] = v;
                pixels[idx + 1] = v;
                pixels[idx + 2] = v;
                pixels[idx + 3] = v;
            }
        }
        return new SpecialGlyphBitmap(GlyphWidth, GlyphHeight, pixels);
    }

    //SpecialUnbakedGlyph 特殊字形 IUnbakedGlyph 实现
    //Bake 调 stitcher.Stitch 缝到 RGBA8 图集返回 SheetBakedGlyph 复用 F7 渲染逻辑
    //Info advance=width+1=6 BoldOffset/ShadowOffset 用 IGlyphInfo 默认 1.0
    private sealed class SpecialUnbakedGlyph : IUnbakedGlyph
    {
        private readonly SpecialGlyphBitmap _bitmap;

        public SpecialUnbakedGlyph(SpecialGlyphBitmap bitmap) => _bitmap = bitmap;

        public IGlyphInfo Info => new SpecialGlyphInfo(GlyphWidth + 1);

        public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher)
            => stitcher.Stitch(Info, _bitmap);
    }

    //SpecialGlyphInfo 仅 advance 度量对标原版 SpecialGlyphs.getAdvance = image.width+1
    private sealed class SpecialGlyphInfo : IGlyphInfo
    {
        public float Advance { get; }
        public SpecialGlyphInfo(float advance) => Advance = advance;
    }

    //SpecialGlyphBitmap 固定 RGBA8 位图实现 IGlyphBitmap
    //Oversample=1.0 IsColored=true 像素由外部传入构造时已生成
    //BearingLeft/BearingTop 用默认 0.0/7.0(Baseline) Top=0 Bottom=8 对标原版 5x8 图像位置
    private sealed class SpecialGlyphBitmap : IGlyphBitmap
    {
        private readonly byte[] _pixels;

        public SpecialGlyphBitmap(int width, int height, byte[] pixels)
        {
            PixelWidth = width;
            PixelHeight = height;
            _pixels = pixels;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public float Oversample => 1.0f;
        public bool IsColored => true;
        public byte[] GetPixels() => _pixels;
    }
}
