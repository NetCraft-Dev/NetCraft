namespace NetCraft.Gpu.Font;

//SpaceGlyphProvider 对标原版 SpaceProvider
//空格只有 advance 无位图 advances Map<int,float> 转 EmptyGlyph 字典
//font/*.json 中 "space" 类型 provider 的 advances 字段定义各空格字符宽度
//例如 U+0020 普通空格 advance=4.0 U+00A0 不间断空格 advance=4.0
public sealed class SpaceGlyphProvider : IGlyphProvider
{
    private readonly Dictionary<int, EmptyGlyph> _glyphs;

    public SpaceGlyphProvider(IReadOnlyDictionary<int, float> advances)
    {
        _glyphs = new Dictionary<int, EmptyGlyph>(advances.Count);
        foreach (var (codepoint, advance) in advances)
            _glyphs[codepoint] = new EmptyGlyph(advance);
    }

    public IReadOnlySet<int> GetSupportedGlyphs() => _supported ??= new HashSet<int>(_glyphs.Keys);

    private HashSet<int>? _supported;

    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        return _glyphs.TryGetValue(codepoint, out var glyph) ? glyph : null;
    }

    public void Dispose() { }
}
