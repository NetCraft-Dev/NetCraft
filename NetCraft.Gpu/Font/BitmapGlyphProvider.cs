using System.Collections.Concurrent;
using StbImageSharp;

namespace NetCraft.Gpu.Font;

//BitmapGlyphProvider 对标原版 BitmapProvider
//从 PNG 纹理按 codepointGrid 网格切割每个字形
//chars 字符串数组每行解码为 codepoint 序列对应纹理网格行
//actualGlyphWidth 从右向左扫描首个非0像素列计算 advance
//pixelScale = height / glyphHeight 把整图坐标换算到字形高度
public sealed unsafe class BitmapGlyphProvider : IGlyphProvider
{
    private readonly byte[] _imageBytes;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly int[][] _codepointGrid;
    private readonly int _height;
    private readonly int _ascent;
    private readonly ConcurrentDictionary<int, IUnbakedGlyph?> _cache = new();

    public BitmapGlyphProvider(byte[] pngBytes, int[][] codepointGrid, int height, int ascent)
    {
        ImageResult image;
        fixed (byte* p = pngBytes)
        {
            image = ImageResult.FromMemory(pngBytes, ColorComponents.RedGreenBlueAlpha);
        }
        _imageBytes = image.Data;
        _imageWidth = image.Width;
        _imageHeight = image.Height;
        _codepointGrid = codepointGrid;
        _height = height;
        _ascent = ascent;
    }

    public IReadOnlySet<int> GetSupportedGlyphs()
    {
        var set = new HashSet<int>();
        foreach (var line in _codepointGrid)
            foreach (var cp in line)
                if (cp != 0) set.Add(cp);
        return set;
    }

    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        if (codepoint == 0) return null;
        return _cache.GetOrAdd(codepoint, LoadGlyph);
    }

    //LoadGlyph 遍历 codepointGrid 找 codepoint 所在行列构造字形度量
    private IUnbakedGlyph? LoadGlyph(int codepoint)
    {
        int glyphWidth = _imageWidth / _codepointGrid[0].Length;
        int glyphHeight = _imageHeight / _codepointGrid.Length;
        float pixelScale = _height / (float)glyphHeight;
        for (int row = 0; row < _codepointGrid.Length; row++)
        {
            var line = _codepointGrid[row];
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] != codepoint) continue;
                int actualWidth = GetActualGlyphWidth(glyphWidth, glyphHeight, col, row);
                int advance = (int)(0.5f + actualWidth * pixelScale) + 1;
                float oversample = 1.0f / pixelScale;
                return new BitmapGlyph(this, col * glyphWidth, row * glyphHeight, glyphWidth, glyphHeight, advance, _ascent, oversample);
            }
        }
        return null;
    }

    //GetActualGlyphWidth 从右向左扫描 glyphWidth 列找首个非0 alpha 像素列
    //对标原版 BitmapProvider.Definition.getActualGlyphWidth
    private int GetActualGlyphWidth(int glyphWidth, int glyphHeight, int slotX, int slotY)
    {
        int width = glyphWidth - 1;
        while (width >= 0)
        {
            int xPixel = slotX * glyphWidth + width;
            for (int y = 0; y < glyphHeight; y++)
            {
                int yPixel = slotY * glyphHeight + y;
                int idx = (yPixel * _imageWidth + xPixel) * 4 + 3;
                if (_imageBytes[idx] != 0) return width + 1;
            }
            width--;
        }
        return width + 1;
    }

    public void Dispose() { }

    //BitmapGlyph 对标原版 BitmapProvider.Glyph 持有偏移/尺寸/advance/ascent
    private sealed class BitmapGlyph : IUnbakedGlyph
    {
        internal readonly BitmapGlyphProvider _owner;
        internal readonly int _offsetX;
        internal readonly int _offsetY;
        internal readonly int _glyphWidth;
        internal readonly int _glyphHeight;
        internal readonly int _advance;
        internal readonly int _ascent;
        internal readonly float _oversample;
        private readonly IGlyphInfo _info;

        public BitmapGlyph(BitmapGlyphProvider owner, int offsetX, int offsetY, int glyphWidth, int glyphHeight, int advance, int ascent, float oversample)
        {
            _owner = owner;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _glyphWidth = glyphWidth;
            _glyphHeight = glyphHeight;
            _advance = advance;
            _ascent = ascent;
            _oversample = oversample;
            _info = IGlyphInfo.Simple(advance);
        }

        public IGlyphInfo Info => _info;

        public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher)
            => stitcher.Stitch(_info, new BitmapGlyphBitmap(this));
    }

    //BitmapGlyphBitmap 对标原版 BitmapProvider.Glyph 的 GlyphBitmap
    //PixelWidth/Height 用完整 glyphWidth/glyphHeight 保持网格对齐
    //GetPixels 从原图裁剪 offsetX/offsetY 起 glyphWidth×glyphHeight 的 RGBA 像素
    private sealed class BitmapGlyphBitmap : IGlyphBitmap
    {
        private readonly BitmapGlyph _glyph;
        private byte[]? _pixels;

        public BitmapGlyphBitmap(BitmapGlyph glyph) => _glyph = glyph;

        public int PixelWidth => _glyph._glyphWidth;
        public int PixelHeight => _glyph._glyphHeight;
        public float Oversample => _glyph._oversample;
        public bool IsColored => true;
        public float BearingLeft => 0f;
        public float BearingTop => _glyph._ascent;

        public byte[] GetPixels()
        {
            if (_pixels != null) return _pixels;
            int w = _glyph._glyphWidth;
            int h = _glyph._glyphHeight;
            var bytes = new byte[w * h * 4];
            int srcStride = _glyph._owner._imageWidth * 4;
            int dstStride = w * 4;
            for (int y = 0; y < h; y++)
            {
                int srcRow = (_glyph._offsetY + y) * srcStride + _glyph._offsetX * 4;
                int dstRow = y * dstStride;
                Buffer.BlockCopy(_glyph._owner._imageBytes, srcRow, bytes, dstRow, dstStride);
            }
            _pixels = bytes;
            return _pixels;
        }
    }
}
