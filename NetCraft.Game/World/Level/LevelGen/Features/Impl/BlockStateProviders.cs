using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl;

//BlockStateProvider block state provider base, maps to vanilla stateproviders.BlockStateProvider
//Gives the block state to place from the position and random source; providers can nest
public abstract class BlockStateProvider
{
    //Codec polymorphic entry dispatching on the type field into the BLOCK_STATE_PROVIDER_TYPE registry
    public static readonly Codec<BlockStateProvider> Codec = new BlockStateProviderDispatchCodec();

    //Type owning type singleton; encoding and registry resolution use it to get the id
    public abstract BlockStateProviderType Type { get; }

    //GetState fetch a definite state, maps to vanilla getState
    public abstract BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos);

    //GetOptionalState may return null, maps to vanilla getOptionalState
    public virtual BlockState? GetOptionalState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => GetState(level, random, pos);

    //Simple convenience constructor for a single state provider, maps to vanilla simple
    public static SimpleStateProvider Simple(BlockState state) => new(state);

    public static SimpleStateProvider Simple(RegBlock block) => new(block.DefaultBlockState);
}

//BlockStateProviderType provider type singleton base, maps to vanilla BlockStateProviderType
//Non-generic base held by the registry, carrying the type id and the map-to-instance decode entry
public abstract class BlockStateProviderType : NetCraft.Registry.BlockStateProviderType<object>
{
    public Identifier Id { get; }

    protected BlockStateProviderType(Identifier id) => Id = id;

    //Decode decode a provider instance from the map; the type field is already consumed by the caller
    public abstract DataResult<BlockStateProvider> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields accumulate the instance fields into the builder; the type field is added by the caller
    public abstract void EncodeFields<U>(DynamicOps<U> ops, BlockStateProvider value, RecordBuilder<U> builder);

    public override string ToString() => $"BlockStateProviderType[{Id}]";
}

//BlockStateProviderType<P> generic middle layer for a concrete provider type; subclasses only supply one MapCodec<P>
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

//SimpleBlockStateProviderType type instance carrying only an id and a codec, covering all built-in providers
internal sealed class SimpleBlockStateProviderType<P> : BlockStateProviderType<P> where P : BlockStateProvider
{
    public SimpleBlockStateProviderType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//BlockStateProviderTypes built-in provider type registration, maps to the static fields of vanilla BlockStateProviderType
//Registering happens as the static fields initialize; RegisterAll just touches them
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

    //Register register into BLOCKSTATE_PROVIDER_TYPE and return the type instance
    private static BlockStateProviderType<T> Register<T>(string path, MapCodec<T> codec) where T : BlockStateProvider
    {
        var type = new SimpleBlockStateProviderType<T>(path, codec);
        Registry<NetCraft.Registry.BlockStateProviderType<object>>.Register(
            BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE, type.Id, type);
        return type;
    }
}

//SimpleStateProvider single state provider, maps to vanilla SimpleStateProvider
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

//WeightedStateProvider weighted state provider, maps to vanilla WeightedStateProvider
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

//RotatedBlockProvider random axis block provider, maps to vanilla RotatedBlockProvider
//Blocks with an axis property such as logs use it for a random orientation
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
        //Vanilla Direction.Axis.getRandom picks one of the XYZ axes uniformly
        var axis = random.NextInt(3) switch
        {
            0 => NetCraft.Registry.Enums.Axis.x,
            1 => NetCraft.Registry.Enums.Axis.y,
            _ => NetCraft.Registry.Enums.Axis.z,
        };
        return Block.DefaultBlockState.TrySetValue(BlockStateProperties.AxisProperty, axis);
    }
}

//RandomizedIntStateProvider randomized int property provider, maps to vanilla RandomizedIntStateProvider
//Fetches the base state then randomly resets the given integer property
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

    //FindIntProperty find an integer property by name; return it unchanged when the name does not match
    private static IntegerProperty? FindIntProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property is IntegerProperty intProperty && intProperty.Name == name)
                return intProperty;
        return null;
    }
}

//RuleBasedStateProvider rule-based state provider, maps to vanilla RuleBasedStateProvider
//Tests rules in order and delegates to the matching provider; the fallback is used only when nothing matches
//The tree below_trunk_provider uses it to decide what fills below the trunk based on block tags
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

    //GetState keep the original block when no rule matches and there is no fallback, matching the fallback of vanilla getState
    public override BlockState GetState(WorldGenRegion level, RandomSource random, BlockPos pos)
        => GetOptionalState(level, random, pos) ?? level.GetBlockState(pos.X, pos.Y, pos.Z);
}

//RuleBasedStateRule one rule of the rule-based state provider, maps to vanilla RuleBasedStateProvider.Rule
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

//BlockStateProviderDispatchCodec look up BLOCKSTATE_PROVIDER_TYPE by the type field then delegate to that type
//Maps to vanilla BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE.byNameCodec().dispatch(...)
internal sealed class BlockStateProviderDispatchCodec : ScalarCodec<BlockStateProvider>
{
    public override DataResult<BlockStateProvider> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));

    private static DataResult<BlockStateProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<BlockStateProvider>.Error(() => "block state provider is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<BlockStateProvider>.Error(() => "block state provider type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<BlockStateProvider>.Error(() => $"invalid provider type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.BLOCKSTATE_PROVIDER_TYPE.GetValue(typeId.Value) is not BlockStateProviderType type)
            return DataResult<BlockStateProvider>.Error(() => $"unknown provider type: {typeId}");
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

//SingleFieldMapCodec single-field codec, maps to the single-field form of vanilla RecordCodecBuilder
//The project's RecordCodecBuilder starts at two fields, so single-field types wrap with this
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

//WeightedListCodec weighted list codec, maps to vanilla WeightedList.codec
//Element form is {"data": <element>, "weight": <non-negative int>}
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
                return DataResult<WeightedList<E>>.Error(() => "weighted list elements must be objects");
            var map = mapResult.GetOrThrow();

            var dataTag = map.Get("data");
            if (!dataTag.IsPresent)
                return DataResult<WeightedList<E>>.Error(() => "weighted list element is missing the data field");
            var valueResult = _element.Parse(ops, dataTag.Get());
            if (!valueResult.Result().IsPresent)
                return DataResult<WeightedList<E>>.Error(() => "weighted list data failed to parse");

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
