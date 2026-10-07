namespace NetCraft.Gpu.Font;

//SpaceGlyphProvider maps to vanilla SpaceProvider
//Spaces have only advance, no bitmap; the advances Map<int,float> converts to an EmptyGlyph dictionary
//The advances field of a "space" provider in font/*.json defines the width of each space character
//e.g. U+0020 regular space advance=4.0, U+00A0 no-break space advance=4.0
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
