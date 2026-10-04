namespace NetCraft.Gpu.Font;

//GlyphProviderType 字形提供器类型枚举对标原版 GlyphProviderType
//dispatch font.json 的 type 字段到对应 provider 的解析器
//Bitmap=PNG位图 Ttf=TrueType Space=空格宽度 Unihex=hex位图 Reference=引用其他provider
public enum GlyphProviderType
{
    Bitmap,
    Ttf,
    Space,
    Unihex,
    Reference
}
