namespace NetCraft.Gpu.Font;

//GlyphProviderType glyph provider type enum, maps to vanilla GlyphProviderType
//Dispatches the type field of font.json to the matching provider parser
//Bitmap=PNG bitmap Ttf=TrueType Space=space width Unihex=hex bitmap Reference=references another provider
public enum GlyphProviderType
{
    Bitmap,
    Ttf,
    Space,
    Unihex,
    Reference
}
