using System.Globalization;
using NetCraft.Codec;
using NetCraft.Registry.Environment;

namespace NetCraft.Registry;

//GrassColorModifier 草色修正对应原版 BiomeSpecialEffects.GrassColorModifier
public enum GrassColorModifier
{
    None,
    DarkForest,
    Swamp
}

//BiomeSpecialEffects 群系视觉效果对应原版 BiomeSpecialEffects
public sealed class BiomeSpecialEffects
{
    //GrassColorModifierCodec 草色修正枚举 codec
    public static readonly Codec<GrassColorModifier> GrassColorModifierCodec = new StringEnumCodec<GrassColorModifier>(
        (GrassColorModifier.None, "none"),
        (GrassColorModifier.DarkForest, "dark_forest"),
        (GrassColorModifier.Swamp, "swamp"));

    public static readonly Codec<BiomeSpecialEffects> Codec = RecordCodecBuilder.Of5<BiomeSpecialEffects, int, Optional<int>, Optional<int>, Optional<int>, GrassColorModifier>(
        HexColorCodec.StringRgb.FieldOf("water_color").ForGetter<BiomeSpecialEffects, int>(e => e.WaterColor),
        HexColorCodec.StringRgb.OptionalFieldOf("foliage_color").ForGetter<BiomeSpecialEffects, Optional<int>>(e => e.FoliageColor),
        HexColorCodec.StringRgb.OptionalFieldOf("dry_foliage_color").ForGetter<BiomeSpecialEffects, Optional<int>>(e => e.DryFoliageColor),
        HexColorCodec.StringRgb.OptionalFieldOf("grass_color").ForGetter<BiomeSpecialEffects, Optional<int>>(e => e.GrassColor),
        GrassColorModifierCodec.OptionalFieldOf("grass_color_modifier", GrassColorModifier.None)
            .ForGetter<BiomeSpecialEffects, GrassColorModifier>(e => e.GrassColorModifier),
        (waterColor, foliageColor, dryFoliageColor, grassColor, modifier) =>
            new BiomeSpecialEffects(waterColor, foliageColor, dryFoliageColor, grassColor, modifier));

    public int WaterColor { get; }

    public Optional<int> FoliageColor { get; }

    public Optional<int> DryFoliageColor { get; }

    public Optional<int> GrassColor { get; }

    public GrassColorModifier GrassColorModifier { get; }

    public BiomeSpecialEffects(int waterColor, Optional<int> foliageColor, Optional<int> dryFoliageColor,
        Optional<int> grassColor, GrassColorModifier grassColorModifier)
    {
        WaterColor = waterColor;
        FoliageColor = foliageColor;
        DryFoliageColor = dryFoliageColor;
        GrassColor = grassColor;
        GrassColorModifier = grassColorModifier;
    }

    //WaterColorHex 网络同步用的十六进制串对应原版 ExtraCodecs.STRING_RGB_COLOR 编码形态
    public string WaterColorHex => "#" + (WaterColor & 0xFFFFFF).ToString("x6", CultureInfo.InvariantCulture);
}
