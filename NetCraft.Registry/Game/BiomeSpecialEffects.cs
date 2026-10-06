using System.Globalization;
using NetCraft.Codec;
using NetCraft.Registry.Environment;

namespace NetCraft.Registry;

//GrassColorModifier grass color modifier, maps to vanilla BiomeSpecialEffects.GrassColorModifier
public enum GrassColorModifier
{
    None,
    DarkForest,
    Swamp
}

//BiomeSpecialEffects biome visual effects, maps to vanilla BiomeSpecialEffects
public sealed class BiomeSpecialEffects
{
    //GrassColorModifierCodec grass color modifier enum codec
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

    //WaterColorHex hex string for network sync, maps to vanilla ExtraCodecs.STRING_RGB_COLOR encoded form
    public string WaterColorHex => "#" + (WaterColor & 0xFFFFFF).ToString("x6", CultureInfo.InvariantCulture);
}
