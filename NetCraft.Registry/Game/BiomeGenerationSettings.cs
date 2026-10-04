using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//BiomeGenerationSettings 群系生成设置对应原版 BiomeGenerationSettings
//carvers 对应 ConfiguredWorldCarver.LIST_CODEC features 对应 PlacedFeature.LIST_OF_LISTS_CODEC
//features 外层按 GenerationStep.Decoration 下标分组 内层是该 step 下本群系引用的已放置特征
public sealed class BiomeGenerationSettings
{
    public static readonly BiomeGenerationSettings Empty = new(
        new DirectHolderSet<ConfiguredWorldCarver>(Array.Empty<Holder<ConfiguredWorldCarver>>()),
        Array.Empty<HolderSet<PlacedFeature>>());

    //FeaturesCodec 外层数组内层数组 codec 空内层数组保留占位步骤
    public static readonly Codec<IReadOnlyList<HolderSet<PlacedFeature>>> FeaturesCodec =
        HolderSetCodecs.PlacedFeatureSet.ListOf();

    public static readonly Codec<BiomeGenerationSettings> Codec =
        RecordCodecBuilder.Of2<BiomeGenerationSettings, HolderSet<ConfiguredWorldCarver>, IReadOnlyList<HolderSet<PlacedFeature>>>(
            HolderSetCodecs.ConfiguredCarverSet.FieldOf("carvers")
                .ForGetter<BiomeGenerationSettings, HolderSet<ConfiguredWorldCarver>>(s => s.Carvers),
            FeaturesCodec.FieldOf("features").ForGetter<BiomeGenerationSettings, IReadOnlyList<HolderSet<PlacedFeature>>>(s => s.Features),
            (carvers, features) => new BiomeGenerationSettings(carvers, features));

    //Carvers 雕刻器集合 按配置化雕刻器注册表绑定
    public HolderSet<ConfiguredWorldCarver> Carvers { get; }

    //Features 逐 step 的已放置特征集合 下标即 GenerationStep.Decoration 序数
    public IReadOnlyList<HolderSet<PlacedFeature>> Features { get; }

    public BiomeGenerationSettings(HolderSet<ConfiguredWorldCarver> carvers, IReadOnlyList<HolderSet<PlacedFeature>> features)
    {
        Carvers = carvers;
        Features = features;
    }
}
