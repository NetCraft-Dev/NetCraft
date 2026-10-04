using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl;

//BlockStateProvider 方块状态提供者基类 对应原版 stateproviders.BlockStateProvider
//按位置与随机源给出要放置的方块状态 提供者之间可嵌套
public abstract class BlockStateProvider
{
    //Codec 多态入口 按 type 字段派发到 BLOCK_STATE_PROVIDER_TYPE 注册表
    public static readonly Codec<BlockStateProvider> Codec = new BlockStateProviderDispatchCodec();

    //Type 所属类型单例 编码与注册表解析靠它拿 id
    public abstract BlockStateProviderType Type { get; }

    //GetState 取一个确定的状态 对应原版 getState
    public abstract BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos);

    //GetOptionalState 允许返回空 对应原版 getOptionalState
    public virtual BlockState? GetOptionalState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => GetState(level, random, pos);

    //Simple 单状态提供者便捷构造 对应原版 simple
    public static SimpleStateProvider Simple(BlockState state) => new(state);

    public static SimpleStateProvider Simple(RegBlock block) => new(block.DefaultBlockState);
}

//BlockStateProviderType 提供者类型单例基类 对应原版 BlockStateProviderType
//非泛型基类供注册表持有 持类型 id 与「map → 实例」的解码入口
public abstract class BlockStateProviderType : NetCraft.Registry.BlockStateProviderType<object>
{
    public Identifier Id { get; }

    protected BlockStateProviderType(Identifier id) => Id = id;

    //Decode 从 map 解出一个提供者实例 type 字段已由外层消费
    public abstract DataResult<BlockStateProvider> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields 把实例参数累积进 builder type 字段由外层补
    public abstract void EncodeFields<U>(DynamicOps<U> ops, BlockStateProvider value, RecordBuilder<U> builder);

    public override string ToString() => $"BlockStateProviderType[{Id}]";
}

//BlockStateProviderType<P> 具体提供者类型的泛型中间层 子类只需给出一个 MapCodec<P>
public abstract class BlockStateProviderType<P> : BlockStateProviderType where P : BlockStateProvider
{
    private readonly MapCodec<P> _codec;

    protected BlockStateProviderType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<BlockStateProvider> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (BlockStateProvider)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, BlockStateProvider value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

//SimpleBlockStateProviderType 只带 id 与 codec 的类型实例 覆盖全部内置提供者
internal sealed class SimpleBlockStateProviderType<P> : BlockStateProviderType<P> where P : BlockStateProvider
{
    public SimpleBlockStateProviderType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//BlockStateProviderTypes 内置提供者类型登记 对应原版 BlockStateProviderType 的静态字段
//静态字段初始化即完成注册 供 RegisterAll 触碰
public static class BlockStateProviderTypes
{
    public static readonly BlockStateProviderType<SimpleStateProvider> Simple =
        Register("simple_state_provider", SimpleStateProvider.MapCodec);

    public static readonly BlockStateProviderType<WeightedStateProvider> Weighted =
        Register("weighted_state_provider", WeightedStateProvider.MapCodec);

    public static readonly BlockStateProviderType<RotatedBlockProvider> Rotated =
        Register("rotated_block_provider", RotatedBlockProvider.MapCodec);

    public static readonly BlockStateProviderType<RandomizedIntStateProvider> RandomizedInt =
        Register("randomized_int_state_provider", RandomizedIntStateProvider.MapCodec);

    public static readonly BlockStateProviderType<RuleBasedStateProvider> RuleBased =
        Register("rule_based_state_provider", RuleBasedStateProvider.MapCodec);

    //Register 登记进 BLOCKSTATE_PROVIDER_TYPE 并返回类型实例
    private static BlockStateProviderType<T> Register<T>(string path, MapCodec<T> codec) where T : BlockStateProvider
    {
        var type = new SimpleBlockStateProviderType<T>(path, codec);
        Registry<NetCraft.Registry.BlockStateProviderType<object>>.Register(
            BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE, type.Id, type);
        return type;
    }
}

//SimpleStateProvider 单状态提供者 对应原版 SimpleStateProvider
public sealed class SimpleStateProvider : BlockStateProvider
{
    public static readonly MapCodec<SimpleStateProvider> MapCodec =
        new SingleFieldMapCodec<SimpleStateProvider, BlockState>(
            BlockStateCodec.Instance.FieldOf("state"),
            state => new SimpleStateProvider(state),
            provider => provider.State);

    public BlockState State { get; }

    public SimpleStateProvider(BlockState state) => State = state;

    public override BlockStateProviderType Type => BlockStateProviderTypes.Simple;

    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos) => State;
}

//WeightedStateProvider 权重状态提供者 对应原版 WeightedStateProvider
public sealed class WeightedStateProvider : BlockStateProvider
{
    public static readonly MapCodec<WeightedStateProvider> MapCodec =
        new SingleFieldMapCodec<WeightedStateProvider, WeightedList<BlockState>>(
            new WeightedListCodec<BlockState>(BlockStateCodec.Instance).FieldOf("entries"),
            list => new WeightedStateProvider(list),
            provider => provider.WeightedList);

    public WeightedList<BlockState> WeightedList { get; }

    public WeightedStateProvider(WeightedList<BlockState> weightedList) => WeightedList = weightedList;

    public override BlockStateProviderType Type => BlockStateProviderTypes.Weighted;

    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => WeightedList.GetRandomOrThrow(random);
}

//RotatedBlockProvider 随机轴方块提供者 对应原版 RotatedBlockProvider
//原木这类带 axis 属性的方块靠它随机朝向
public sealed class RotatedBlockProvider : BlockStateProvider
{
    public static readonly MapCodec<RotatedBlockProvider> MapCodec =
        new SingleFieldMapCodec<RotatedBlockProvider, BlockState>(
            BlockStateCodec.Instance.FieldOf("state"),
            state => new RotatedBlockProvider(state.Owner),
            provider => provider.Block.DefaultBlockState);

    public RegBlock Block { get; }

    public RotatedBlockProvider(RegBlock block) => Block = block;

    public override BlockStateProviderType Type => BlockStateProviderTypes.Rotated;

    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        //原版 Direction.Axis.getRandom 在 XYZ 三轴里等概率取一个
        var axis = random.NextInt(3) switch
        {
            0 => NetCraft.Registry.Enums.Axis.x,
            1 => NetCraft.Registry.Enums.Axis.y,
            _ => NetCraft.Registry.Enums.Axis.z,
        };
        return Block.DefaultBlockState.TrySetValue(BlockStateProperties.AxisProperty, axis);
    }
}

//RandomizedIntStateProvider 随机整型属性提供者 对应原版 RandomizedIntStateProvider
//先取基础状态再把指定整型属性随机重设
public sealed class RandomizedIntStateProvider : BlockStateProvider
{
    public static readonly MapCodec<RandomizedIntStateProvider> MapCodec =
        RecordCodecBuilder.Of3<RandomizedIntStateProvider, BlockStateProvider, string, IntProvider>(
            BlockStateProvider.Codec.FieldOf("source")
                .ForGetter<RandomizedIntStateProvider, BlockStateProvider>(p => p.Source),
            Codecs.String.FieldOf("property").ForGetter<RandomizedIntStateProvider, string>(p => p.PropertyName),
            IntProviders.Codec.FieldOf("values").ForGetter<RandomizedIntStateProvider, IntProvider>(p => p.Values),
            (source, propertyName, values) => new RandomizedIntStateProvider(source, propertyName, values));

    public BlockStateProvider Source { get; }
    public string PropertyName { get; }
    public IntProvider Values { get; }

    public RandomizedIntStateProvider(BlockStateProvider source, string propertyName, IntProvider values)
    {
        Source = source;
        PropertyName = propertyName;
        Values = values;
    }

    public override BlockStateProviderType Type => BlockStateProviderTypes.RandomizedInt;

    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        var state = Source.GetState(level, random, pos);
        var property = FindIntProperty(state, PropertyName);
        if (property is null) return state;
        return state.SetValue(property, (object)Values.Sample(random));
    }

    //FindIntProperty 按属性名找整型属性 属性名对不上时原样返回
    private static IntegerProperty? FindIntProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property is IntegerProperty intProperty && intProperty.Name == name)
                return intProperty;
        return null;
    }
}

//RuleBasedStateProvider 规则状态提供者 对应原版 RuleBasedStateProvider
//按规则顺序逐条测试 命中就交给对应提供者 都没命中且有 fallback 才用 fallback
//树木的 below_trunk_provider 靠它按方块标签决定树干下方填什么
public sealed class RuleBasedStateProvider : BlockStateProvider
{
    public static readonly MapCodec<RuleBasedStateProvider> MapCodec =
        RecordCodecBuilder.Of2<RuleBasedStateProvider, Optional<BlockStateProvider>,
            IReadOnlyList<RuleBasedStateRule>>(
            BlockStateProvider.Codec.OptionalFieldOf("fallback")
                .ForGetter<RuleBasedStateProvider, Optional<BlockStateProvider>>(
                    p => Optional<BlockStateProvider>.OfNullable(p.Fallback)),
            RuleBasedStateRule.Codec.ListOf().FieldOf("rules")
                .ForGetter<RuleBasedStateProvider, IReadOnlyList<RuleBasedStateRule>>(p => p.Rules),
            (fallback, rules) => new RuleBasedStateProvider(
                fallback.IsPresent ? fallback.Get() : null, rules));

    public BlockStateProvider? Fallback { get; }
    public IReadOnlyList<RuleBasedStateRule> Rules { get; }

    public RuleBasedStateProvider(BlockStateProvider? fallback, IReadOnlyList<RuleBasedStateRule> rules)
    {
        Fallback = fallback;
        Rules = rules;
    }

    public override BlockStateProviderType Type => BlockStateProviderTypes.RuleBased;

    public override BlockState? GetOptionalState(WorldGenRegion level, RandomSource random, BlockPos pos)
    {
        foreach (var rule in Rules)
            if (rule.IfTrue.Test(level, pos))
                return rule.Then.GetState(level, random, pos);
        return Fallback?.GetState(level, random, pos);
    }

    //GetState 无规则命中且没有 fallback 时保留原方块 对应原版 getState 的兜底
    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => GetOptionalState(level, random, pos) ?? level.GetBlockState(pos.X, pos.Y, pos.Z);
}

//RuleBasedStateRule 规则状态提供者的一条规则 对应原版 RuleBasedStateProvider.Rule
public sealed class RuleBasedStateRule
{
    public static readonly Codec<RuleBasedStateRule> Codec =
        RecordCodecBuilder.Of2<RuleBasedStateRule, BlockPredicate, BlockStateProvider>(
            BlockPredicate.Codec.FieldOf("if_true")
                .ForGetter<RuleBasedStateRule, BlockPredicate>(rule => rule.IfTrue),
            BlockStateProvider.Codec.FieldOf("then")
                .ForGetter<RuleBasedStateRule, BlockStateProvider>(rule => rule.Then),
            (ifTrue, then) => new RuleBasedStateRule(ifTrue, then));

    public BlockPredicate IfTrue { get; }
    public BlockStateProvider Then { get; }

    public RuleBasedStateRule(BlockPredicate ifTrue, BlockStateProvider then)
    {
        IfTrue = ifTrue;
        Then = then;
    }
}

//BlockStateProviderDispatchCodec 按 type 字段查 BLOCKSTATE_PROVIDER_TYPE 再委派给该类型
//对应原版 BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE.byNameCodec().dispatch(...)
internal sealed class BlockStateProviderDispatchCodec : ScalarCodec<BlockStateProvider>
{
    public override DataResult<BlockStateProvider> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));

    private static DataResult<BlockStateProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<BlockStateProvider>.Error(() => "方块状态提供者缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<BlockStateProvider>.Error(() => "方块状态提供者的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<BlockStateProvider>.Error(() => $"非法的提供者类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE.GetValue(typeId.Value) is not BlockStateProviderType type)
            return DataResult<BlockStateProvider>.Error(() => $"未知的提供者类型: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, BlockStateProvider value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}

//SingleFieldMapCodec 单字段 codec 对应原版 RecordCodecBuilder 单字段形态
//项目 RecordCodecBuilder 从两字段起 单字段的类型用这个包装
internal sealed class SingleFieldMapCodec<T, F> : AbstractMapCodec<T>
{
    private readonly MapCodec<F> _field;
    private readonly Func<F, T> _ctor;
    private readonly Func<T, F> _getter;

    public SingleFieldMapCodec(MapCodec<F> field, Func<F, T> ctor, Func<T, F> getter)
    {
        _field = field;
        _ctor = ctor;
        _getter = getter;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _field.Decode(ops, input).Map(_ctor);

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => _field.EncodeTo(ops, _getter(value), builder);
}

//WeightedListCodec 权重列表编解码 对应原版 WeightedList.codec
//元素形态 {"data": <元素>, "weight": <非负整数>}
internal sealed class WeightedListCodec<E> : ScalarCodec<WeightedList<E>>
{
    private readonly Codec<E> _element;

    public WeightedListCodec(Codec<E> element) => _element = element;

    public override DataResult<WeightedList<E>> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStream(input).FlatMap(stream => DecodeEntries(ops, stream));

    private DataResult<WeightedList<E>> DecodeEntries<U>(DynamicOps<U> ops, IEnumerable<U> stream)
    {
        var entries = new List<Weighted<E>>();
        foreach (var element in stream)
        {
            var mapResult = ops.GetMap(element);
            if (!mapResult.Result().IsPresent)
                return DataResult<WeightedList<E>>.Error(() => "权重列表的元素必须是对象");
            var map = mapResult.GetOrThrow();

            var dataTag = map.Get("data");
            if (!dataTag.IsPresent)
                return DataResult<WeightedList<E>>.Error(() => "权重列表的元素缺 data 字段");
            var valueResult = _element.Parse(ops, dataTag.Get());
            if (!valueResult.Result().IsPresent)
                return DataResult<WeightedList<E>>.Error(() => "权重列表的 data 解析失败");

            var weight = 1;
            var weightTag = map.Get("weight");
            if (weightTag.IsPresent)
            {
                var weightResult = ops.GetNumberValue(weightTag.Get());
                if (weightResult.Result().IsPresent) weight = (int)weightResult.GetOrThrow();
            }
            entries.Add(new Weighted<E>(valueResult.GetOrThrow(), weight));
        }
        return DataResult<WeightedList<E>>.Success(WeightedList<E>.Of(entries));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, WeightedList<E> value)
    {
        var encoded = new List<U>();
        foreach (var entry in value.Unwrap())
        {
            var dataResult = _element.EncodeStart(ops, entry.Value);
            if (!dataResult.Result().IsPresent) return dataResult;
            var builder = ops.MapBuilder();
            builder.Add("data", dataResult.GetOrThrow());
            builder.Add("weight", ops.CreateInt(entry.Weight));
            var built = builder.Build(ops.Empty());
            if (!built.Result().IsPresent) return built;
            encoded.Add(built.GetOrThrow());
        }
        return DataResult<U>.Success(ops.CreateList(encoded));
    }
}
