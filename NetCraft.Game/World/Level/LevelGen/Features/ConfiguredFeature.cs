using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//ConfiguredFeature 配置化特征 非泛型化对应原版 ConfiguredFeature<FC,F>
//把特征单例与其配置绑成注册表元素 类型名由 type 字段派发 config 走各特征自己的 codec
public sealed class ConfiguredFeature : NetCraft.Registry.ConfiguredFeature
{
    //Codec 元素 codec 对应原版 DIRECT_CODEC
    public static readonly Codec<ConfiguredFeature> Codec = new ConfiguredFeatureCodec();

    //ElementCodec 注册表元素 codec 注册表按标记接口持有元素
    public static readonly Codec<NetCraft.Registry.ConfiguredFeature> ElementCodec = Codec.ComapFlatMap(
        feature => DataResult<NetCraft.Registry.ConfiguredFeature>.Success(feature),
        feature => (ConfiguredFeature)feature);

    public Feature Feature { get; }
    public FeatureConfiguration Config { get; }

    public ConfiguredFeature(Feature feature, FeatureConfiguration config)
    {
        Feature = feature;
        Config = config;
    }

    //Place 放置本特征对应原版 place
    public bool Place(WorldGenRegion level, ChunkGenerator generator, RandomSource random, BlockPos origin)
        => Feature.Place(Config, new FeaturePlaceContext(level, generator, random, origin, Config));

    //SubFeatures 本配置内嵌的子特征引用
    public IEnumerable<Holder<NetCraft.Registry.ConfiguredFeature>> SubFeatures => Config.SubFeatures;

    public override string ToString() => $"{Feature.Id}[{Config.GetType().Name}]";
}

//ConfiguredFeatureCodec 按 type 字段查 FEATURE 注册表 再按 config 字段解配置
//对应原版 BuiltInRegistries.FEATURE.byNameCodec().dispatch(f => f, f => f.configuredCodec())
internal sealed class ConfiguredFeatureCodec : ScalarCodec<ConfiguredFeature>
{
    public override DataResult<ConfiguredFeature> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeFeature(ops, map));

    private static DataResult<ConfiguredFeature> DecodeFeature<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<ConfiguredFeature>.Error(() => "configured_feature 缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<ConfiguredFeature>.Error(() => "configured_feature 的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<ConfiguredFeature>.Error(() => $"非法的特征类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.FEATURE.GetValue(typeId.Value) is not Feature feature)
            return DataResult<ConfiguredFeature>.Error(() => $"未知的特征类型: {typeId}");

        var configTag = input.Get("config");
        if (!configTag.IsPresent)
            return DataResult<ConfiguredFeature>.Error(() => $"特征 {typeId} 缺 config 字段");
        return feature.ConfigCodec.Parse(ops, configTag.Get()).Map(config => new ConfiguredFeature(feature, config));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, ConfiguredFeature value)
    {
        var configResult = value.Feature.ConfigCodec.EncodeStart(ops, value.Config);
        if (!configResult.Result().IsPresent) return configResult;
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Feature.Id.ToString()));
        builder.Add("config", configResult.GetOrThrow());
        return builder.Build(ops.Empty());
    }
}
