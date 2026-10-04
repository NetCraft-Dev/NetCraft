using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using StbTrueTypeSharp;

namespace NetCraft.Gpu.Font;

//TtfGlyphProvider 对标原版 TrueTypeGlyphProvider
//原版用 FreeType 我们用 StbTrueTypeSharp 包装 API 形态不同但度量等价
//GCHandle Pinned 固定 TTF 字节 stbtt_fontinfo 内部 data 指针长生命周期有效
public sealed unsafe class TtfGlyphProvider : IGlyphProvider
{
    private readonly byte[] _ttfBytes;
    private readonly GCHandle _ttfPin;
    private readonly StbTrueType.stbtt_fontinfo _fontInfo;
    private readonly float _oversample;
    private readonly float _shiftX;
    private readonly float _shiftY;
    private readonly HashSet<int> _skip;
    private readonly float _scale;
    private readonly ConcurrentDictionary<int, IUnbakedGlyph?> _cache = new();

    //BMP 范围作为 GetSupportedGlyphs 占位 StbTrueType 无 cmap 枚举 API
    //实际 GetGlyph 时用 stbtt_FindGlyphIndex 判断 codepoint 是否真有字形
    private static readonly HashSet<int> s_bmpRange = BuildBmpRange();

    public TtfGlyphProvider(byte[] ttfBytes, float size, float oversample, float shiftX, float shiftY, string skip)
    {
        _ttfBytes = ttfBytes;
        _oversample = oversample;
        _shiftX = shiftX;
        _shiftY = shiftY;
        _skip = new HashSet<int>();
        foreach (var rune in skip.EnumerateRunes())
            _skip.Add(rune.Value);

        _ttfPin = GCHandle.Alloc(ttfBytes, GCHandleType.Pinned);
        _fontInfo = new StbTrueType.stbtt_fontinfo();
        var dataPtr = (byte*)_ttfPin.AddrOfPinnedObject();
        if (StbTrueType.stbtt_InitFont(_fontInfo, dataPtr, 0) == 0)
        {
            _ttfPin.Free();
            throw new InvalidOperationException("stbtt_InitFont 失败");
        }
        //对标原版 FT_Set_Pixel_Sizes(size * oversample)
        _scale = StbTrueType.stbtt_ScaleForPixelHeight(_fontInfo, size * oversample);
    }

    public IReadOnlySet<int> GetSupportedGlyphs() => s_bmpRange;

    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        if (_skip.Contains(codepoint)) return null;
        return _cache.GetOrAdd(codepoint, LoadGlyph);
    }

    //LoadGlyph 懒加载字形对标原版 loadGlyph
    //stbtt_GetGlyphHMetrics 返回字体单位 advance 乘 scale 得 oversample 像素再除 oversample 得逻辑像素
    //stbtt 坐标系 y 向下为正 原版 bitmap_top 向上为正 bearingTop 取反 stbtt y0
    private IUnbakedGlyph? LoadGlyph(int codepoint)
    {
        int index = StbTrueType.stbtt_FindGlyphIndex(_fontInfo, codepoint);
        if (index == 0) return null;

        int advance = 0, leftBearing = 0;
        StbTrueType.stbtt_GetGlyphHMetrics(_fontInfo, index, &advance, &leftBearing);
        float scaledAdvance = advance * _scale;

        float subX = _shiftX * _oversample;
        float subY = -_shiftY * _oversample;
        int x0, y0, x1, y1;
        StbTrueType.stbtt_GetGlyphBitmapBoxSubpixel(_fontInfo, index, _scale, _scale, subX, subY, &x0, &y0, &x1, &y1);
        int width = x1 - x0;
        int height = y1 - y0;

        if (width <= 0 || height <= 0)
            return new EmptyGlyph(scaledAdvance / _oversample);

        float bearingX = x0 / _oversample;
        float bearingY = -y0 / _oversample;
        return new TtfGlyph(this, index, width, height, scaledAdvance, bearingX, bearingY);
    }

    //Rasterize 栅格化单个字形到 R8 byte[]
    //TtfGlyphBitmap.GetPixels 首次调用时调此方法结果缓存
    internal byte[] Rasterize(int index, int width, int height)
    {
        var pixels = new byte[width * height];
        float subX = _shiftX * _oversample;
        float subY = -_shiftY * _oversample;
        fixed (byte* p = pixels)
        {
            StbTrueType.stbtt_MakeGlyphBitmapSubpixel(_fontInfo, p, width, height, width, _scale, _scale, subX, subY, index);
        }
        return pixels;
    }

    private static HashSet<int> BuildBmpRange()
    {
        var set = new HashSet<int>(0xFFFF - 0x20 + 1);
        for (int cp = 0x20; cp <= 0xFFFF; cp++)
            set.Add(cp);
        return set;
    }

    public void Dispose()
    {
        if (_ttfPin.IsAllocated)
            _ttfPin.Free();
    }

    //TtfGlyph 单字形未烘焙对象对标原版 TrueTypeGlyphProvider.Glyph
    //持有 stbtt glyph index 和度量 Bake 时构造 TtfGlyphBitmap 懒栅格化
    //字段 internal 供兄弟嵌套类 TtfGlyphBitmap 访问 C# 嵌套类不互访 private
    private sealed class TtfGlyph : IUnbakedGlyph
    {
        internal readonly TtfGlyphProvider _owner;
        internal readonly int _index;
        internal readonly int _width;
        internal readonly int _height;
        internal readonly float _bearingX;
        internal readonly float _bearingY;
        internal readonly IGlyphInfo _info;

        public TtfGlyph(TtfGlyphProvider owner, int index, int width, int height, float advance, float bearingX, float bearingY)
        {
            _owner = owner;
            _index = index;
            _width = width;
            _height = height;
            _bearingX = bearingX;
            _bearingY = bearingY;
            _info = IGlyphInfo.Simple(advance / owner._oversample);
        }

        public IGlyphInfo Info => _info;

        public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher)
            => stitcher.Stitch(_info, new TtfGlyphBitmap(this));
    }

    //TtfGlyphBitmap 单字形栅格化位图对标原版 TrueTypeGlyphProvider.Glyph.1
    //GetPixels 首次调用时调 owner.Rasterize 懒栅格化并缓存
    private sealed class TtfGlyphBitmap : IGlyphBitmap
    {
        private readonly TtfGlyph _glyph;
        private byte[]? _pixels;

        public TtfGlyphBitmap(TtfGlyph glyph) => _glyph = glyph;

        public int PixelWidth => _glyph._width;
        public int PixelHeight => _glyph._height;
        public float Oversample => _glyph._owner._oversample;
        public bool IsColored => false;
        public float BearingLeft => _glyph._bearingX;
        public float BearingTop => _glyph._bearingY;

        public byte[] GetPixels()
        {
            if (_pixels == null)
                _pixels = _glyph._owner.Rasterize(_glyph._index, _glyph._width, _glyph._height);
            return _pixels;
        }
    }
}
