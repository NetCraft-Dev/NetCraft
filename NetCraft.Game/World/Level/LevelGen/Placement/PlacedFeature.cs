using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;
using GameConfiguredFeature = NetCraft.Game.World.Level.LevelGen.Features.ConfiguredFeature;
using RegistryConfiguredFeature = NetCraft.Registry.ConfiguredFeature;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacedFeature 已放置特征对应原版 PlacedFeature
//把一个配置化特征与一串放置修饰器绑在一起 装饰时先算位置再逐个尝试放置
public sealed class PlacedFeature : NetCraft.Registry.PlacedFeature
{
    //Codec 元素 codec 对应原版 DIRECT_CODEC
    public static readonly Codec<PlacedFeature> Codec = new PlacedFeatureCodec();

    //ElementCodec 注册表元素 codec 注册表按标记接口持有元素
    public static readonly Codec<NetCraft.Registry.PlacedFeature> ElementCodec = Codec.ComapFlatMap(
        feature => DataResult<NetCraft.Registry.PlacedFeature>.Success(feature),
        feature => (PlacedFeature)feature);

    //Feature 引用的配置化特征
    public Holder<RegistryConfiguredFeature> Feature { get; }

    //Placement 放置修饰器链 依次作用得到最终候选位置
    public IReadOnlyList<PlacementModifier> Placement { get; }

    public PlacedFeature(Holder<RegistryConfiguredFeature> feature, IReadOnlyList<PlacementModifier> placement)
    {
        Feature = feature;
        Placement = placement;
    }

    //Place 放置本特征对应原版 place
    public bool Place(WorldGenRegion level, ChunkGenerator generator, RandomSource random, BlockPos origin)
        => PlaceWithContext(new PlacementContext(level, generator, null), random, origin);

    //PlaceWithBiomeCheck 带群系校验的放置对应原版 placeWithBiomeCheck
    //装饰链路走这个入口 让 biome 修饰器能反查本特征属于哪些群系
    public bool PlaceWithBiomeCheck(WorldGenRegion level, ChunkGenerator generator, RandomSource random, BlockPos origin)
        => PlaceWithContext(new PlacementContext(level, generator, this), random, origin);

    //PlaceWithContext 先跑修饰器链得到候选位置 再逐点尝试放置
    private bool PlaceWithContext(PlacementContext context, RandomSource random, BlockPos origin)
    {
        IEnumerable<BlockPos> positions = new[] { origin };
        foreach (var modifier in Placement)
        {
            var source = positions;
            var current = modifier;
            positions = source.SelectMany(pos => current.GetPositions(context, random, pos));
        }

        var placedAny = false;
        foreach (var pos in positions)
        {
            if (Feature.Value is GameConfiguredFeature configured
                && configured.Place(context.Level, context.Generator, random, pos))
                placedAny = true;
        }
        return placedAny;
    }

    //GetFeatures 本特征引用的全部配置化特征 含配置内嵌的子特征
    public IEnumerable<Holder<RegistryConfiguredFeature>> GetFeatures()
    {
        yield return Feature;
        if (Feature.Value is GameConfiguredFeature configured)
        {
            foreach (var sub in configured.SubFeatures) yield return sub;
        }
    }

    public override string ToString() => $"Placed {Feature.RegisteredName}";
}

//PlacedFeatureCodec 解 {feature, placement} 对应原版 DIRECT_CODEC
//feature 是 registered 引用字符串 placement 是修饰器数组
internal sealed class PlacedFeatureCodec : ScalarCodec<PlacedFeature>
{
    public override DataResult<PlacedFeature> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePlacedFeature(ops, map));

    private static DataResult<PlacedFeature> DecodePlacedFeature<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var featureTag = input.Get("feature");
        if (!featureTag.IsPresent) return DataResult<PlacedFeature>.Error(() => "placed_feature 缺 feature 字段");
        var featureResult = ConfiguredFeatureInlineRefCodec.Instance.Parse(ops, featureTag.Get());
        if (!featureResult.Result().IsPresent)
            return DataResult<PlacedFeature>.Error(() => "placed_feature 的 feature 解析失败: "
                + featureResult.MapOrElse(_ => string.Empty, error => error));

        var placementTag = input.Get("placement");
        if (!placementTag.IsPresent)
            return DataResult<PlacedFeature>.Error(() => "placed_feature 缺 placement 字段");
        var streamResult = ops.GetStream(placementTag.Get());
        if (!streamResult.Result().IsPresent)
            return DataResult<PlacedFeature>.Error(() => "placed_feature 的 placement 必须是数组");

        var modifiers = new List<PlacementModifier>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var modifierResult = PlacementModifierCodec.Instance.Parse(ops, element);
            if (!modifierResult.Result().IsPresent)
                return DataResult<PlacedFeature>.Error(() => $"placed_feature 第 {modifiers.Count} 个放置修饰器解析失败: "
                    + modifierResult.MapOrElse(_ => string.Empty, error => error));
            modifiers.Add(modifierResult.GetOrThrow());
        }

        return DataResult<PlacedFeature>.Success(new PlacedFeature(featureResult.GetOrThrow(), modifiers));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PlacedFeature value)
    {
        var featureResult = ConfiguredFeatureInlineRefCodec.Instance.EncodeStart(ops, value.Feature);
        if (!featureResult.Result().IsPresent) return featureResult;
        var modifiers = new List<U>();
        foreach (var modifier in value.Placement)
        {
            var modifierResult = PlacementModifierCodec.Instance.EncodeStart(ops, modifier);
            if (!modifierResult.Result().IsPresent) return modifierResult;
            modifiers.Add(modifierResult.GetOrThrow());
        }

        var builder = ops.MapBuilder();
        builder.Add("feature", featureResult.GetOrThrow());
        builder.Add("placement", ops.CreateList(modifiers));
        return builder.Build(ops.Empty());
    }
}

//ConfiguredFeatureInlineRefCodec 配置化特征引用编解码 注册名与内联定义都接受
//对应原版 ConfiguredFeature.CODEC 的 allowInline 形态
//placed_feature 的 feature 字段既可能是 "minecraft:oak" 也可能是整段内联定义
internal sealed class ConfiguredFeatureInlineRefCodec : ScalarCodec<Holder<RegistryConfiguredFeature>>
{
    public static readonly ConfiguredFeatureInlineRefCodec Instance = new();

    public override DataResult<Holder<RegistryConfiguredFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent) return HolderSetCodecs.ConfiguredFeatureRef.Parse(ops, input);
        var inline = GameConfiguredFeature.Codec.Parse(ops, input);
        if (inline.Result().IsPresent)
            return DataResult<Holder<RegistryConfiguredFeature>>.Success(
                Holder<RegistryConfiguredFeature>.Direct(inline.GetOrThrow()));
        var reason = inline.MapOrElse(_ => string.Empty, error => error);
        return DataResult<Holder<RegistryConfiguredFeature>>.Error(
            () => $"配置化特征既不是注册名也不是内联定义: {reason}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryConfiguredFeature> value)
        => value.UnwrapKey() is null && value.Value is GameConfiguredFeature inline
            ? GameConfiguredFeature.Codec.EncodeStart(ops, inline)
            : HolderSetCodecs.ConfiguredFeatureRef.EncodeStart(ops, value);
}
