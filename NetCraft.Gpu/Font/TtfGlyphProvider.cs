using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using StbTrueTypeSharp;

namespace NetCraft.Gpu.Font;

//TtfGlyphProvider maps to vanilla TrueTypeGlyphProvider
//Vanilla uses FreeType; we use StbTrueTypeSharp, a different API shape but equivalent metrics
//GCHandle Pinned pins the TTF bytes so the stbtt_fontinfo internal data pointer stays valid for a long lifetime
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

    //The BMP range is a placeholder for GetSupportedGlyphs; StbTrueType has no cmap enumeration API
    //GetGlyph actually uses stbtt_FindGlyphIndex to check whether a codepoint really has a glyph
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
            throw new InvalidOperationException("stbtt_InitFont failed");
        }
        //maps to vanilla FT_Set_Pixel_Sizes(size * oversample)
        _scale = StbTrueType.stbtt_ScaleForPixelHeight(_fontInfo, size * oversample);
    }

    public IReadOnlySet<int> GetSupportedGlyphs() => s_bmpRange;

    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        if (_skip.Contains(codepoint)) return null;
        return _cache.GetOrAdd(codepoint, LoadGlyph);
    }

    //LoadGlyph lazily loads a glyph, maps to vanilla loadGlyph
    //stbtt_GetGlyphHMetrics returns the advance in font units; multiply by scale for oversampled pixels, then divide by oversample for logical pixels
    //stbtt's coordinate system has positive y downward; vanilla bitmap_top is positive upward, so bearingTop negates stbtt y0
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

    //Rasterize rasterizes a single glyph into an R8 byte[]
    //TtfGlyphBitmap.GetPixels calls this on first use and caches the result
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

    //TtfGlyph single unbaked glyph object, maps to vanilla TrueTypeGlyphProvider.Glyph
    //Holds the stbtt glyph index and metrics; Bake builds a TtfGlyphBitmap for lazy rasterization
    //Fields are internal so the sibling nested class TtfGlyphBitmap can access them; C# nested classes cannot access each other's private members
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

    //TtfGlyphBitmap single-glyph rasterized bitmap, maps to vanilla TrueTypeGlyphProvider.Glyph.1
    //GetPixels calls owner.Rasterize on first use, lazily rasterizing and caching
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
