using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen;

//SurfaceRulesCodecs 表面规则 codec 集合对应原版 SurfaceRules.ConditionSource.CODEC 与 RuleSource.CODEC
//按 type 字段 dispatch 到各子类型 codec 编码时按运行时类型写回 type
public static class SurfaceRulesCodecs
{
    //ConditionSourceCodec 条件源 dispatch codec
    public static readonly Codec<SurfaceRules.ConditionSource> ConditionSourceCodec =
        new ConditionSourceDispatchCodec();

    //RuleSourceCodec 规则源 dispatch codec
    public static readonly Codec<SurfaceRules.RuleSource> RuleSourceCodec =
        new RuleSourceDispatchCodec();
}

//ConditionSourceDispatchCodec 条件源 type 派发编解码
internal sealed class ConditionSourceDispatchCodec : AbstractMapCodec<SurfaceRules.ConditionSource>
{
    private readonly IReadOnlyDictionary<string, MapCodec<SurfaceRules.ConditionSource>> _codecs;

    public ConditionSourceDispatchCodec()
    {
        var codecs = new Dictionary<string, MapCodec<SurfaceRules.ConditionSource>>
        {
            ["biome"] = new MappedMapCodec<SurfaceRules.BiomeCondition, SurfaceRules.ConditionSource>(
                BiomeListCodec.Instance.FieldOf("biome_is"),
                condition => condition,
                condition => (SurfaceRules.BiomeCondition)condition),
            ["noise_threshold"] = RecordCodecBuilder.Of4<SurfaceRules.ConditionSource, NoiseHolder, double, double, bool>(
                NoiseHolderCodec.Instance.FieldOf("noise")
                    .ForGetter<SurfaceRules.ConditionSource, NoiseHolder>(c => ((SurfaceRules.NoiseThreshold)c).Noise),
                Codecs.Double.FieldOf("min_threshold")
                    .ForGetter<SurfaceRules.ConditionSource, double>(c => ((SurfaceRules.NoiseThreshold)c).MinThreshold),
                Codecs.Double.FieldOf("max_threshold")
                    .ForGetter<SurfaceRules.ConditionSource, double>(c => ((SurfaceRules.NoiseThreshold)c).MaxThreshold),
                Codecs.Bool.OptionalFieldOf("is_3d", false)
                    .ForGetter<SurfaceRules.ConditionSource, bool>(c => ((SurfaceRules.NoiseThreshold)c).Is3D),
                (noise, min, max, is3D)
                    => (SurfaceRules.ConditionSource)new SurfaceRules.NoiseThreshold(noise, min, max, is3D)),
            ["vertical_gradient"] = RecordCodecBuilder.Of3<SurfaceRules.ConditionSource, Identifier, VerticalAnchor, VerticalAnchor>(
                IdentifierCodec.Instance.FieldOf("random_name")
                    .ForGetter<SurfaceRules.ConditionSource, Identifier>(c => ((SurfaceRules.VerticalGradient)c).RandomName),
                VerticalAnchor.Codec.FieldOf("true_at_and_below")
                    .ForGetter<SurfaceRules.ConditionSource, VerticalAnchor>(c => ((SurfaceRules.VerticalGradient)c).TrueAtAndBelow),
                VerticalAnchor.Codec.FieldOf("false_at_and_above")
                    .ForGetter<SurfaceRules.ConditionSource, VerticalAnchor>(c => ((SurfaceRules.VerticalGradient)c).FalseAtAndAbove),
                (name, trueAt, falseAt)
                    => (SurfaceRules.ConditionSource)new SurfaceRules.VerticalGradient(trueAt, falseAt, name)),
            ["y_above"] = RecordCodecBuilder.Of3<SurfaceRules.ConditionSource, VerticalAnchor, int, bool>(
                VerticalAnchor.Codec.FieldOf("anchor")
                    .ForGetter<SurfaceRules.ConditionSource, VerticalAnchor>(c => ((SurfaceRules.YAbove)c).Anchor),
                Codecs.Int.FieldOf("surface_depth_multiplier")
                    .ForGetter<SurfaceRules.ConditionSource, int>(c => ((SurfaceRules.YAbove)c).SurfaceDepthMultiplier),
                Codecs.Bool.FieldOf("add_stone_depth")
                    .ForGetter<SurfaceRules.ConditionSource, bool>(c => ((SurfaceRules.YAbove)c).AddStoneDepth),
                (anchor, multiplier, addStoneDepth)
                    => (SurfaceRules.ConditionSource)new SurfaceRules.YAbove(anchor, multiplier, addStoneDepth)),
            ["water"] = RecordCodecBuilder.Of3<SurfaceRules.ConditionSource, int, int, bool>(
                Codecs.Int.FieldOf("offset")
                    .ForGetter<SurfaceRules.ConditionSource, int>(c => ((SurfaceRules.Water)c).Offset),
                Codecs.Int.FieldOf("surface_depth_multiplier")
                    .ForGetter<SurfaceRules.ConditionSource, int>(c => ((SurfaceRules.Water)c).SurfaceDepthMultiplier),
                Codecs.Bool.FieldOf("add_stone_depth")
                    .ForGetter<SurfaceRules.ConditionSource, bool>(c => ((SurfaceRules.Water)c).AddStoneDepth),
                (offset, multiplier, addStoneDepth)
                    => (SurfaceRules.ConditionSource)new SurfaceRules.Water(offset, addStoneDepth, multiplier)),
            ["temperature"] = new UnitMapCodec<SurfaceRules.ConditionSource>(() => new SurfaceRules.Temperature()),
            ["steep"] = new UnitMapCodec<SurfaceRules.ConditionSource>(() => new SurfaceRules.Steep()),
            ["hole"] = new UnitMapCodec<SurfaceRules.ConditionSource>(() => new SurfaceRules.Hole()),
            ["above_preliminary_surface"] =
                new UnitMapCodec<SurfaceRules.ConditionSource>(() => new SurfaceRules.AbovePreliminarySurface()),
            ["stone_depth"] = RecordCodecBuilder.Of4<SurfaceRules.ConditionSource, int, bool, int, SurfaceRules.SurfaceType>(
                Codecs.Int.FieldOf("offset")
                    .ForGetter<SurfaceRules.ConditionSource, int>(c => ((SurfaceRules.StoneDepth)c).Offset),
                Codecs.Bool.FieldOf("add_surface_depth")
                    .ForGetter<SurfaceRules.ConditionSource, bool>(c => ((SurfaceRules.StoneDepth)c).AddSurfaceDepth),
                Codecs.Int.FieldOf("secondary_depth_range")
                    .ForGetter<SurfaceRules.ConditionSource, int>(c => ((SurfaceRules.StoneDepth)c).SecondaryDepthRange),
                SurfaceTypeCodec.Instance.FieldOf("surface_type")
                    .ForGetter<SurfaceRules.ConditionSource, SurfaceRules.SurfaceType>(c => ((SurfaceRules.StoneDepth)c).Surface),
                (offset, addSurfaceDepth, secondaryDepthRange, surfaceType)
                    => (SurfaceRules.ConditionSource)new SurfaceRules.StoneDepth(offset, addSurfaceDepth, secondaryDepthRange, surfaceType))
        };
        //not 内嵌自身条件源 codec 对应原版 ConditionSource.CODEC.xmap(...).fieldOf("invert")
        codecs["not"] = new MappedMapCodec<SurfaceRules.ConditionSource, SurfaceRules.ConditionSource>(
            this.FieldOf("invert"),
            inner => new SurfaceRules.Not(inner),
            condition => ((SurfaceRules.Not)condition).Inner);
        _codecs = codecs;
    }

    public override DataResult<SurfaceRules.ConditionSource> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = SurfaceRulesCodecHelper.ReadType(ops, input);
        if (!type.IsPresent)
            return DataResult<SurfaceRules.ConditionSource>.Error(() => "missing type field for ConditionSource");
        var codec = Lookup(_codecs, type.Get());
        if (codec is null)
            return DataResult<SurfaceRules.ConditionSource>.Error(() => $"unknown condition type: {type.Get()}");
        var decoded = codec.Decode(ops, input);
        //内层失败原因必须带出来 否则嵌套规则报错只剩顶层类型名 定位不到是哪一层
        var cause = string.Empty;
        var value = decoded.ResultOrPartial(msg => cause = msg);
        return value.IsPresent
            ? DataResult<SurfaceRules.ConditionSource>.Success(value.Get())
            : DataResult<SurfaceRules.ConditionSource>.Error(() => $"failed to decode condition type {type.Get()}: {cause}");
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, SurfaceRules.ConditionSource value, RecordBuilder<U> builder)
    {
        var name = ConditionSourceNames.NameOf(value);
        var codec = Lookup(_codecs, name);
        if (codec is null)
            return builder.Add("type", DataResult<U>.Error(() => $"unsupported ConditionSource: {value.GetType().Name}").GetOrThrow());
        builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace(name).ToString()));
        return codec.EncodeTo(ops, value, builder);
    }

    //Lookup 按 type 名取子 codec 名字带命名空间时只取路径段
    internal static MapCodec<T>? Lookup<T>(IReadOnlyDictionary<string, MapCodec<T>> codecs, string type)
    {
        var id = Identifier.TryParse(type);
        var path = id is not null ? id.Value.Path : type;
        return codecs.TryGetValue(path, out var codec) ? codec : null;
    }
}

//RuleSourceDispatchCodec 规则源 type 派发编解码
internal sealed class RuleSourceDispatchCodec : AbstractMapCodec<SurfaceRules.RuleSource>
{
    private readonly IReadOnlyDictionary<string, MapCodec<SurfaceRules.RuleSource>> _codecs;

    public RuleSourceDispatchCodec()
    {
        _codecs = new Dictionary<string, MapCodec<SurfaceRules.RuleSource>>
        {
            ["block"] = new MappedMapCodec<NetCraft.Registry.State.BlockState, SurfaceRules.RuleSource>(
                BlockStateCodec.Instance.FieldOf("result_state"),
                state => new SurfaceRules.BlockStateRule(state),
                rule => ((SurfaceRules.BlockStateRule)rule).State),
            ["sequence"] = new MappedMapCodec<IReadOnlyList<SurfaceRules.RuleSource>, SurfaceRules.RuleSource>(
                this.ListOf().FieldOf("sequence"),
                rules => new SurfaceRules.Sequence(rules),
                rule => ((SurfaceRules.Sequence)rule).Rules),
            ["condition"] = RecordCodecBuilder.Of2<SurfaceRules.RuleSource, SurfaceRules.ConditionSource, SurfaceRules.RuleSource>(
                SurfaceRulesCodecs.ConditionSourceCodec.FieldOf("if_true")
                    .ForGetter<SurfaceRules.RuleSource, SurfaceRules.ConditionSource>(r => ((SurfaceRules.IfTrue)r).Condition),
                this.FieldOf("then_run")
                    .ForGetter<SurfaceRules.RuleSource, SurfaceRules.RuleSource>(r => ((SurfaceRules.IfTrue)r).Then),
                (condition, then) => (SurfaceRules.RuleSource)new SurfaceRules.IfTrue(condition, then)),
            ["bandlands"] = new UnitMapCodec<SurfaceRules.RuleSource>(() => new SurfaceRules.Bandlands())
        };
    }

    public override DataResult<SurfaceRules.RuleSource> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = SurfaceRulesCodecHelper.ReadType(ops, input);
        if (!type.IsPresent)
            return DataResult<SurfaceRules.RuleSource>.Error(() => "missing type field for RuleSource");
        var codec = ConditionSourceDispatchCodec.Lookup(_codecs, type.Get());
        if (codec is null)
            return DataResult<SurfaceRules.RuleSource>.Error(() => $"unknown rule type: {type.Get()}");
        var decoded = codec.Decode(ops, input);
        //内层失败原因必须带出来 否则嵌套规则报错只剩顶层类型名 定位不到是哪一层
        var cause = string.Empty;
        var value = decoded.ResultOrPartial(msg => cause = msg);
        return value.IsPresent
            ? DataResult<SurfaceRules.RuleSource>.Success(value.Get())
            : DataResult<SurfaceRules.RuleSource>.Error(() => $"failed to decode rule type {type.Get()}: {cause}");
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, SurfaceRules.RuleSource value, RecordBuilder<U> builder)
    {
        //空规则编码为空 sequence 供未配置表面规则的配置往返
        if (value is null)
        {
            builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("sequence").ToString()));
            return builder.Add("sequence", ops.CreateList(Array.Empty<U>()));
        }
        var name = RuleSourceNames.NameOf(value);
        var codec = ConditionSourceDispatchCodec.Lookup(_codecs, name);
        if (codec is null)
            return builder.Add("type", DataResult<U>.Error(() => $"unsupported RuleSource: {value.GetType().Name}").GetOrThrow());
        builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace(name).ToString()));
        return codec.EncodeTo(ops, value, builder);
    }
}

//SurfaceRulesCodecHelper 读取 type 字段
internal static class SurfaceRulesCodecHelper
{
    //ReadType 读 type 字符串字段 缺失或非字符串返回空
    public static Optional<string> ReadType<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var tag = input.Get("type");
        if (!tag.IsPresent) return Optional<string>.Empty();
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? Optional<string>.Of(text.GetOrThrow()) : Optional<string>.Empty();
    }
}

//ConditionSourceNames 条件源运行时类型到 type 名映射对应原版注册名
internal static class ConditionSourceNames
{
    public static string NameOf(SurfaceRules.ConditionSource value) => value switch
    {
        SurfaceRules.BiomeCondition => "biome",
        SurfaceRules.NoiseThreshold => "noise_threshold",
        SurfaceRules.VerticalGradient => "vertical_gradient",
        SurfaceRules.YAbove => "y_above",
        SurfaceRules.Water => "water",
        SurfaceRules.Temperature => "temperature",
        SurfaceRules.Steep => "steep",
        SurfaceRules.Not => "not",
        SurfaceRules.Hole => "hole",
        SurfaceRules.AbovePreliminarySurface => "above_preliminary_surface",
        SurfaceRules.StoneDepth => "stone_depth",
        _ => throw new NotSupportedException($"Unsupported ConditionSource: {value.GetType().Name}")
    };
}

//RuleSourceNames 规则源运行时类型到 type 名映射对应原版注册名
internal static class RuleSourceNames
{
    public static string NameOf(SurfaceRules.RuleSource value) => value switch
    {
        SurfaceRules.Bandlands => "bandlands",
        SurfaceRules.BlockStateRule => "block",
        SurfaceRules.Sequence => "sequence",
        SurfaceRules.IfTrue => "condition",
        _ => throw new NotSupportedException($"Unsupported RuleSource: {value.GetType().Name}")
    };
}

//MappedMapCodec MapCodec 级 xmap 对应原版 MapCodec.xmap
internal sealed class MappedMapCodec<TIn, TOut> : AbstractMapCodec<TOut>
{
    private readonly MapCodec<TIn> _inner;
    private readonly Func<TIn, TOut> _to;
    private readonly Func<TOut, TIn> _from;

    public MappedMapCodec(MapCodec<TIn> inner, Func<TIn, TOut> to, Func<TOut, TIn> from)
    {
        _inner = inner;
        _to = to;
        _from = from;
    }

    public override DataResult<TOut> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _inner.Decode(ops, input).Map(_to);

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, TOut value, RecordBuilder<U> builder)
        => _inner.EncodeTo(ops, _from(value), builder);
}

//UnitMapCodec 无字段 codec 对应原版 MapCodec.unit
internal sealed class UnitMapCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public UnitMapCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => builder;
}

//SurfaceTypeCodec 石头深度朝向 codec 对应原版 CaveSurface.CODEC
internal sealed class SurfaceTypeCodec : ScalarCodec<SurfaceRules.SurfaceType>
{
    public static readonly SurfaceTypeCodec Instance = new();

    public override DataResult<SurfaceRules.SurfaceType> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<SurfaceRules.SurfaceType>.Error(() => "surface_type must be a string");
        return text.GetOrThrow() switch
        {
            "floor" => DataResult<SurfaceRules.SurfaceType>.Success(SurfaceRules.SurfaceType.Floor),
            "ceiling" => DataResult<SurfaceRules.SurfaceType>.Success(SurfaceRules.SurfaceType.Ceiling),
            var other => DataResult<SurfaceRules.SurfaceType>.Error(() => $"unknown surface_type: {other}")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, SurfaceRules.SurfaceType value)
        => DataResult<U>.Success(ops.CreateString(value == SurfaceRules.SurfaceType.Floor ? "floor" : "ceiling"));
}

//BiomeListCodec 群系条件 codec 对应原版 RegistryCodecs.homogeneousList(Registries.BIOME)
//三种形态: 单个群系 id / 群系 id 列表 / "#命名空间:标签"
internal sealed class BiomeListCodec : ScalarCodec<SurfaceRules.BiomeCondition>
{
    public static readonly BiomeListCodec Instance = new();

    public override DataResult<SurfaceRules.BiomeCondition> Parse<U>(DynamicOps<U> ops, U input)
    {
        var single = ops.GetStringValue(input);
        if (single.Result().IsPresent)
        {
            var text = single.GetOrThrow();
            if (text.StartsWith('#')) return ResolveTag(ops, text[1..]);
            var resolved = ResolveBiome(ops, text);
            if (!resolved.Result().IsPresent)
                return DataResult<SurfaceRules.BiomeCondition>.Error(() => $"unknown biome: {text}");
            return DataResult<SurfaceRules.BiomeCondition>.Success(
                new SurfaceRules.BiomeCondition(new[] { resolved.GetOrThrow() }));
        }
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent)
            return DataResult<SurfaceRules.BiomeCondition>.Error(
                () => "biome_is must be a string or a string list");
        var list = new List<Biome>();
        foreach (var element in stream.GetOrThrow())
        {
            var text = ops.GetStringValue(element);
            if (!text.Result().IsPresent)
                return DataResult<SurfaceRules.BiomeCondition>.Error(() => "biome_is entries must be strings");
            var resolved = ResolveBiome(ops, text.GetOrThrow());
            if (!resolved.Result().IsPresent)
                return DataResult<SurfaceRules.BiomeCondition>.Error(() => $"unknown biome: {text.GetOrThrow()}");
            list.Add(resolved.GetOrThrow());
        }
        return DataResult<SurfaceRules.BiomeCondition>.Success(new SurfaceRules.BiomeCondition(list));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, SurfaceRules.BiomeCondition value)
    {
        if (value.Tag is not null)
            return DataResult<U>.Success(ops.CreateString("#" + value.Tag.Location));
        if (value.Biomes.Length == 1)
            return DataResult<U>.Success(ops.CreateString(value.Biomes[0].Id.ToString()));
        return DataResult<U>.Success(ops.CreateList(value.Biomes.Select(b => ops.CreateString(b.Id.ToString()))));
    }

    //ResolveTag 解析 "#命名空间:标签"
    //原版对数据包里没出现过的标签是宽容的 这里同样先接受 判定时按未绑定处理
    private static DataResult<SurfaceRules.BiomeCondition> ResolveTag<U>(DynamicOps<U> ops, string text)
    {
        var id = Identifier.TryParse(text);
        if (id is null)
            return DataResult<SurfaceRules.BiomeCondition>.Error(() => $"invalid biome tag: {text}");
        var registry = ResolveRegistry(ops);
        if (registry is null)
            return DataResult<SurfaceRules.BiomeCondition>.Error(() => "biome tag needs a biome registry");
        return DataResult<SurfaceRules.BiomeCondition>.Success(
            new SurfaceRules.BiomeCondition(TagKey<Biome>.Create(Registries.BIOME, id.Value), registry));
    }

    //ResolveRegistry 取群系注册表 优先 RegistryOps 携带的回退内置注册表
    private static Registry<Biome>? ResolveRegistry<U>(DynamicOps<U> ops)
        => ops is RegistryOps<U> registryOps
            ? registryOps.GetRegistry(Registries.BIOME) ?? BuiltInRegistries.BIOME
            : BuiltInRegistries.BIOME;

    //ResolveBiome 优先 RegistryOps 携带的 BIOME 注册表回退内置注册表
    private static DataResult<Biome> ResolveBiome<U>(DynamicOps<U> ops, string text)
    {
        var id = Identifier.TryParse(text);
        if (id is null)
            return DataResult<Biome>.Error(() => $"invalid biome identifier: {text}");
        var registry = ResolveRegistry(ops);
        return registry is not null && registry.ContainsKey(id.Value) && registry.GetValue(id.Value) is { } value
            ? DataResult<Biome>.Success(value)
            : DataResult<Biome>.Error(() => $"unknown biome: {text}");
    }
}
