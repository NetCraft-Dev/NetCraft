using System.Collections.Concurrent;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace NetCraft.Gpu.Font;

//UnihexGlyphProvider 对标原版 UnihexProvider
//hex_file 是 zip 包内含 *.hex 文件每行 <codepoint_hex>:<bitmap_hex>
//codepoint_hex 4-6 位十六进制 bitmap_hex 32/64/96/128 位对应 8/16/24/32 像素宽度
//16 行构成 16 像素高字形按位扫描 set=0xFFFFFFFF clear=0
//Oversample=2.0 IsColored=true advance=width/2+1 BoldOffset/ShadowOffset=0.5
public sealed class UnihexGlyphProvider : IGlyphProvider
{
    private readonly ConcurrentDictionary<int, UnihexGlyph?> _glyphs = new();

    //LoadFromStream 从 zip 或裸 .hex 流加载所有字形
    //zip 时遍历 entries 解析 *.hex 文件否则按裸 .hex 处理
    public static UnihexGlyphProvider LoadFromStream(Stream input)
    {
        var provider = new UnihexGlyphProvider();
        if (IsZipStream(input))
        {
            input.Position = 0;
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in zip.Entries)
            {
                if (!entry.Name.EndsWith(".hex", StringComparison.OrdinalIgnoreCase)) continue;
                using var es = entry.Open();
                ReadHexStream(es, provider);
            }
        }
        else
        {
            input.Position = 0;
            ReadHexStream(input, provider);
        }
        return provider;
    }

    //IsZipStream 检测 magic number PK\x03\x04 判断是否 zip
    private static bool IsZipStream(Stream input)
    {
        if (!input.CanSeek) return false;
        if (input.Length < 4) return false;
        long pos = input.Position;
        int b0 = input.ReadByte();
        int b1 = input.ReadByte();
        int b2 = input.ReadByte();
        int b3 = input.ReadByte();
        input.Position = pos;
        return b0 == 0x50 && b1 == 0x4B && b2 == 0x03 && b3 == 0x04;
    }

    //ReadHexStream 逐行解析 .hex 内容存入 provider
    private static void ReadHexStream(Stream stream, UnihexGlyphProvider provider)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        int lineNo = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            ParseLine(line, lineNo, provider);
        }
    }

    //ParseLine 解析单行 <codepoint_hex>:<bitmap_hex>
    //codepoint 4-6 位 hex bitmap 32/64/96/128 位 hex 对应宽度 8/16/24/32
    private static void ParseLine(string line, int lineNo, UnihexGlyphProvider provider)
    {
        int colon = line.IndexOf(':');
        if (colon < 4 || colon > 6) ThrowInvalid(lineNo, "expected 4, 5 or 6 hex digits followed by a colon");
        int codepoint = 0;
        for (int i = 0; i < colon; i++)
        {
            int v = DecodeHex(line[i], lineNo);
            codepoint = (codepoint << 4) | v;
        }
        string bitmap = line.Substring(colon + 1);
        int bits = bitmap.Length;
        int bitWidth;
        switch (bits)
        {
            case 32: bitWidth = 8; break;
            case 64: bitWidth = 16; break;
            case 96: bitWidth = 24; break;
            case 128: bitWidth = 32; break;
            default: ThrowInvalid(lineNo, "expected hex number describing (8,16,24,32) x 16 bitmap"); bitWidth = 0; break;
        }
        int[] contents = new int[16];
        int bytesPerLine = bitWidth / 8;
        for (int i = 0; i < 16; i++)
        {
            int value = 0;
            for (int j = 0; j < bytesPerLine; j++)
            {
                int idx = i * bytesPerLine * 2 + j * 2;
                int hi = DecodeHex(bitmap[idx], lineNo);
                int lo = DecodeHex(bitmap[idx + 1], lineNo);
                value = (value << 8) | (hi << 4) | lo;
            }
            //每行 int 左移到 32 位高位对标原版 ByteContents<<24/ShortContents<<16/IntContents24<<8
            int shift = 32 - bitWidth;
            contents[i] = value << shift;
        }
        provider._glyphs[codepoint] = new UnihexGlyph(contents);
    }

    private static int DecodeHex(char c, int lineNo)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        ThrowInvalid(lineNo, "expected hex digit, got " + c);
        return 0;
    }

    private static void ThrowInvalid(int lineNo, string msg)
        => throw new ArgumentException($"Invalid entry at line {lineNo}: {msg}");

    private UnihexGlyphProvider() { }

    public IReadOnlySet<int> GetSupportedGlyphs() => _glyphs.Keys.ToHashSet();

    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        return _glyphs.TryGetValue(codepoint, out var glyph) ? glyph : null;
    }

    public void Dispose() { }

    //UnihexGlyph 对标原版 UnihexProvider.Glyph
    //contents 16 行 int 数据 left/right 通过 mask 计算实际像素范围
    private sealed class UnihexGlyph : IUnbakedGlyph
    {
        //字段改 internal 因 C# 嵌套类不互访 private 与 Java 内部类不同
        //UnihexGlyphBitmap 需读 _contents/_left/_right 解包像素对标原版 Glyph.2
        internal readonly int[] _contents;
        internal readonly int _left;
        internal readonly int _right;
        private readonly IGlyphInfo _info;

        public UnihexGlyph(int[] contents)
        {
            _contents = contents;
            int mask = 0;
            for (int i = 0; i < 16; i++) mask |= _contents[i];
            if (mask == 0)
            {
                _left = 0;
                _right = 31;
            }
            else
            {
                _left = BitOperations.LeadingZeroCount((uint)mask);
                _right = 31 - BitOperations.TrailingZeroCount((uint)mask);
            }
            int width = _right - _left + 1;
            float advance = width / 2.0f + 1.0f;
            _info = new UnihexGlyphInfo(advance);
        }

        public IGlyphInfo Info => _info;

        public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher)
            => stitcher.Stitch(_info, new UnihexGlyphBitmap(this));
    }

    //UnihexGlyphInfo 对标原版 UnihexProvider.Glyph.1
    //BoldOffset/ShadowOffset=0.5 非 IGlyphInfo 默认 1.0
    private sealed class UnihexGlyphInfo : IGlyphInfo
    {
        public float Advance { get; }
        public float BoldOffset => 0.5f;
        public float ShadowOffset => 0.5f;

        public UnihexGlyphInfo(float advance) => Advance = advance;
    }

    //UnihexGlyphBitmap 对标原版 UnihexProvider.Glyph.2
    //PixelHeight=16 Oversample=2.0 IsColored=true
    //GetPixels 把 16 行按 left/right 范围解包为 RGBA 像素 set=0xFFFFFFFF clear=0
    private sealed class UnihexGlyphBitmap : IGlyphBitmap
    {
        private readonly UnihexGlyph _glyph;
        private byte[]? _pixels;

        public UnihexGlyphBitmap(UnihexGlyph glyph) => _glyph = glyph;

        public int PixelWidth => _glyph._right - _glyph._left + 1;
        public int PixelHeight => 16;
        public float Oversample => 2.0f;
        public bool IsColored => true;

        public byte[] GetPixels()
        {
            if (_pixels != null) return _pixels;
            int width = PixelWidth;
            var bytes = new byte[width * 16 * 4];
            int startBit = 31 - _glyph._left;
            int endBit = 31 - _glyph._right;
            for (int row = 0; row < 16; row++)
            {
                int value = _glyph._contents[row];
                for (int col = 0; col < width; col++)
                {
                    int bit = startBit - col;
                    bool isSet = bit >= 0 && bit < 32 && ((value >> bit) & 1) != 0;
                    int idx = (row * width + col) * 4;
                    if (isSet)
                    {
                        bytes[idx] = 0xFF;
                        bytes[idx + 1] = 0xFF;
                        bytes[idx + 2] = 0xFF;
                        bytes[idx + 3] = 0xFF;
                    }
                }
            }
            _pixels = bytes;
            return _pixels;
        }
    }
}
