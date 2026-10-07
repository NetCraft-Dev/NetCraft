using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Util.Random;
using RegistryAliasBinding = NetCraft.Registry.PoolAliasBinding;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PoolAliasBinding pool alias binding, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.alias.PoolAliasBinding
//Rewrites one pool name into another; generation first resolves the alias mapping from the seed and generation point, then looks up the pool
//A record is needed for the three concrete aliases to inherit; vanilla uses an interface here
public abstract record PoolAliasBinding : RegistryAliasBinding
{
    //Codec alias polymorphic codec, dispatches by the type field, maps to vanilla PoolAliasBinding.CODEC
    public static readonly Codec<PoolAliasBinding> Codec = new PoolAliasDispatchCodec();

    //ForEachResolved resolves this binding into a set of alias-to-target-pool mappings, maps to vanilla forEachResolved
    public abstract void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer);

    //AllTargets every target pool this binding can point to, maps to vanilla allTargets; used to register target pool placeholders
    public abstract IEnumerable<Identifier> AllTargets();
}

//DirectPoolAlias direct alias, maps to vanilla DirectPoolAlias
//The alias always points to the same target pool and does not involve randomness
public sealed record DirectPoolAlias(Identifier Alias, Identifier Target) : PoolAliasBinding
{
    public static new readonly Codec<DirectPoolAlias> Codec =
        RecordCodecBuilder.Of2<DirectPoolAlias, Identifier, Identifier>(
            StructurePoolCodecs.TemplateLocation.FieldOf("alias").ForGetter<DirectPoolAlias, Identifier>(a => a.Alias),
            StructurePoolCodecs.TemplateLocation.FieldOf("target").ForGetter<DirectPoolAlias, Identifier>(a => a.Target),
            (alias, target) => new DirectPoolAlias(alias, target));

    public override void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer)
        => consumer(Alias, Target);

    public override IEnumerable<Identifier> AllTargets() => new[] { Target };

    public override string ToString() => $"DirectPoolAlias[{Alias}->{Target}]";
}

//RandomPoolAlias random alias, maps to vanilla RandomPoolAlias
//The alias points to a target pool at random by weight; the mapping resolved for the same generation point is fixed
public sealed record RandomPoolAlias(Identifier Alias, WeightedList<Identifier> Targets) : PoolAliasBinding
{
    public static new readonly Codec<RandomPoolAlias> Codec =
        RecordCodecBuilder.Of2<RandomPoolAlias, Identifier, WeightedList<Identifier>>(
            StructurePoolCodecs.TemplateLocation.FieldOf("alias").ForGetter<RandomPoolAlias, Identifier>(a => a.Alias),
            PoolAliasCodecs.WeightedIdentifierList.FieldOf("targets")
                .ForGetter<RandomPoolAlias, WeightedList<Identifier>>(a => a.Targets),
            (alias, targets) => new RandomPoolAlias(alias, targets));

    public override void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer)
        => consumer(Alias, Targets.GetRandomOrThrow(random));

    public override IEnumerable<Identifier> AllTargets()
        => Targets.Unwrap().Select(entry => entry.Value);

    public override string ToString() => $"RandomPoolAlias[{Alias}->{Targets.Unwrap().Count} targets]";
}

//RandomGroupPoolAlias random group alias, maps to vanilla RandomGroupPoolAlias
//Picks a group of aliases by weight and applies the whole group; the bindings within a group share one random source, matching vanilla's pick-group-then-resolve-each
public sealed record RandomGroupPoolAlias(WeightedList<List<PoolAliasBinding>> Groups) : PoolAliasBinding
{
    public static new readonly Codec<RandomGroupPoolAlias> Codec =
        SingleFieldRecordCodec.Of<RandomGroupPoolAlias, WeightedList<List<PoolAliasBinding>>>(
            PoolAliasCodecs.WeightedAliasGroups.FieldOf("groups")
                .ForGetter<RandomGroupPoolAlias, WeightedList<List<PoolAliasBinding>>>(a => a.Groups),
            groups => new RandomGroupPoolAlias(groups));

    public override void ForEachResolved(RandomSource random, Action<Identifier, Identifier> consumer)
    {
        foreach (var binding in Groups.GetRandomOrThrow(random))
            binding.ForEachResolved(random, consumer);
    }

    public override IEnumerable<Identifier> AllTargets()
        => Groups.Unwrap().SelectMany(entry => entry.Value).SelectMany(binding => binding.AllTargets());

    public override string ToString() => $"RandomGroupPoolAlias[{Groups.Unwrap().Count} groups]";
}

//PoolAliasBindings alias type registration, maps to vanilla PoolAliasBindings.bootstrap
public static class PoolAliasBindings
{
    //RegisterAll registers the three alias codecs into POOL_ALIAS_BINDING_TYPE, idempotently
    public static void RegisterAll()
    {
        Register("direct", DirectPoolAlias.Codec);
        Register("random", RandomPoolAlias.Codec);
        Register("random_group", RandomGroupPoolAlias.Codec);
    }

    //Register registers an alias codec by short name; the concrete type is wrapped to adapt to the interface shape the registry requires
    private static void Register<T>(string path, MapCodec<T> codec) where T : PoolAliasBinding
    {
        var id = Identifier.WithDefaultNamespace(path);
        if (BuiltInRegistries.POOL_ALIAS_BINDING_TYPE.ContainsKey(id)) return;
        Registry<MapCodec<RegistryAliasBinding>>.Register(BuiltInRegistries.POOL_ALIAS_BINDING_TYPE, id,
            new PoolAliasBindingMapCodec<T>(codec));
    }
}

//PoolAliasBindingMapCodec adapts a concrete alias's map codec to the interface shape the registry holds
//Generic Decode cannot be variant directly, so a wrapper does the type conversion
internal sealed class PoolAliasBindingMapCodec<T> : MapCodec<RegistryAliasBinding> where T : PoolAliasBinding
{
    private readonly MapCodec<T> _inner;

    public PoolAliasBindingMapCodec(MapCodec<T> inner) => _inner = inner;

    public DataResult<RegistryAliasBinding> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _inner.Decode<U>(ops, input).Map(binding => (RegistryAliasBinding)binding!);

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RegistryAliasBinding value)
        => value is T binding
            ? _inner.EncodeStart<U>(ops, binding)
            : DataResult<U>.Error(() => "alias type does not match this codec");

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, RegistryAliasBinding value, RecordBuilder<U> builder)
        => value is T binding ? _inner.EncodeTo<U>(ops, binding, builder) : builder;

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => ops.MapBuilder();
}

//SingleFieldRecordCodec single-field record codec; vanilla RecordCodecBuilder needs at least two fields, so a one-field record uses this
internal static class SingleFieldRecordCodec
{
    public static Codec<T> Of<T, F>(FieldCodec<T, F> field, Func<F, T> ctor)
        => new SingleFieldRecordCodecImpl<T, F>(field, ctor);
}

internal sealed class SingleFieldRecordCodecImpl<T, F> : AbstractMapCodec<T>
{
    private readonly FieldCodec<T, F> _field;
    private readonly Func<F, T> _ctor;

    public SingleFieldRecordCodecImpl(FieldCodec<T, F> field, Func<F, T> ctor)
    {
        _field = field;
        _ctor = ctor;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _field.Codec.Decode<U>(ops, input).Map(value => _ctor(value!));

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
    {
        _field.Codec.EncodeTo<U>(ops, _field.Getter(value), builder);
        return builder;
    }
}

//PoolAliasCodecs shared codec pieces of the alias subsystem
internal static class PoolAliasCodecs
{
    //WeightedIdentifierList non-empty weighted identifier list, maps to vanilla WeightedList.nonEmptyCodec
    public static readonly Codec<WeightedList<Identifier>> WeightedIdentifierList =
        new WeightedListCodec<Identifier>(StructurePoolCodecs.TemplateLocation, true);

    //WeightedAliasGroups non-empty weighted alias group list, maps to vanilla WeightedList.nonEmptyCodec(Codec.list(PoolAliasBinding.CODEC))
    public static readonly Codec<WeightedList<List<PoolAliasBinding>>> WeightedAliasGroups =
        new WeightedListCodec<List<PoolAliasBinding>>(new PoolAliasListCodec(), true);
}

//PoolAliasListCodec alias list codec; a failure on any entry fails the whole thing without throwing
internal sealed class PoolAliasListCodec : ScalarCodec<List<PoolAliasBinding>>
{
    public override DataResult<List<PoolAliasBinding>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent) return DataResult<List<PoolAliasBinding>>.Error(() => "alias group must be an array");
        var result = new List<PoolAliasBinding>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = PoolAliasBinding.Codec.Parse(ops, element);
            if (!parsed.Result().IsPresent)
                return DataResult<List<PoolAliasBinding>>.Error(() => "failed to parse an alias in the alias group");
            result.Add(parsed.GetOrThrow());
        }
        return DataResult<List<PoolAliasBinding>>.Success(result);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, List<PoolAliasBinding> value)
    {
        var encoded = new List<U>();
        foreach (var binding in value)
        {
            var one = PoolAliasBinding.Codec.EncodeStart(ops, binding);
            if (!one.Result().IsPresent) return DataResult<U>.Error(() => "alias encoding failed");
            encoded.Add(one.GetOrThrow());
        }
        return DataResult<U>.Success(ops.CreateList(encoded));
    }
}

//WeightedListCodec weighted list codec; an element can be a bare value or a data object carrying weight
internal sealed class WeightedListCodec<E> : ScalarCodec<WeightedList<E>>
{
    private readonly Codec<E> _elementCodec;
    private readonly bool _nonEmpty;

    public WeightedListCodec(Codec<E> elementCodec, bool nonEmpty)
    {
        _elementCodec = elementCodec;
        _nonEmpty = nonEmpty;
    }

    public override DataResult<WeightedList<E>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var stream = ops.GetStream(input);
        if (!stream.Result().IsPresent) return DataResult<WeightedList<E>>.Error(() => "weighted list must be an array");

        var entries = new List<Weighted<E>>();
        foreach (var element in stream.GetOrThrow())
        {
            var entry = ReadEntry(ops, element);
            if (!entry.Result().IsPresent) return DataResult<WeightedList<E>>.Error(() => "failed to parse the data of a weighted entry");
            entries.Add(entry.GetOrThrow());
        }
        if (_nonEmpty && entries.Count == 0)
            return DataResult<WeightedList<E>>.Error(() => "weighted list must have at least one element");
        return DataResult<WeightedList<E>>.Success(WeightedList<E>.Of(entries));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, WeightedList<E> value)
    {
        var encoded = new List<U>();
        foreach (var entry in value.Unwrap())
        {
            var data = _elementCodec.EncodeStart(ops, entry.Value);
            if (!data.Result().IsPresent) return DataResult<U>.Error(() => "failed to encode the data of a weighted entry");
            var builder = ops.MapBuilder();
            builder.Add("data", data.GetOrThrow());
            builder.Add("weight", ops.CreateInt(entry.Weight));
            var built = builder.Build(ops.Empty());
            if (!built.Result().IsPresent) return DataResult<U>.Error(() => "weighted entry encoding failed");
            encoded.Add(built.GetOrThrow());
        }
        return DataResult<U>.Success(ops.CreateList(encoded));
    }

    //ReadEntry reads one weighted entry; with a data field it reads as an object, otherwise as a bare value, maps to the two sides of vanilla either
    private DataResult<Weighted<E>> ReadEntry<U>(DynamicOps<U> ops, U element)
    {
        var mapResult = ops.GetMap(element);
        if (mapResult.Result().IsPresent && mapResult.GetOrThrow().Get("data").IsPresent)
        {
            var map = mapResult.GetOrThrow();
            var weightTag = map.Get("weight");
            var weight = 1;
            if (weightTag.IsPresent)
            {
                var number = ops.GetNumberValue(weightTag.Get());
                if (!number.Result().IsPresent) return DataResult<Weighted<E>>.Error(() => "weight must be a number");
                weight = (int)number.GetOrThrow();
                if (weight <= 0) return DataResult<Weighted<E>>.Error(() => $"weight must be positive, got {weight}");
            }
            var dataTag = map.Get("data");
            return _elementCodec.Parse(ops, dataTag.Get()).Map(e => new Weighted<E>(e, weight));
        }
        return _elementCodec.Parse(ops, element).Map(e => new Weighted<E>(e, 1));
    }
}

//PoolAliasDispatchCodec looks up POOL_ALIAS_BINDING_TYPE by the type field then dispatches, maps to vanilla dispatch codec
internal sealed class PoolAliasDispatchCodec : ScalarCodec<PoolAliasBinding>
{
    public override DataResult<PoolAliasBinding> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<PoolAliasBinding>.Error(() => "pool alias must be an object");
        var map = mapResult.GetOrThrow();

        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return DataResult<PoolAliasBinding>.Error(() => "pool alias is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<PoolAliasBinding>.Error(() => "pool alias type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<PoolAliasBinding>.Error(() => $"invalid pool alias type: {typeText.GetOrThrow()}");

        var codec = BuiltInRegistries.POOL_ALIAS_BINDING_TYPE.GetValue(typeId.Value);
        if (codec is null) return DataResult<PoolAliasBinding>.Error(() => $"unregistered pool alias type: {typeId}");

        return codec.Decode<U>(ops, map).FlatMap(binding => binding is PoolAliasBinding gameBinding
            ? DataResult<PoolAliasBinding>.Success(gameBinding)
            : DataResult<PoolAliasBinding>.Error(() => $"pool alias {typeId} is not a Game layer implementation"));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PoolAliasBinding value)
        => value switch
        {
            DirectPoolAlias direct => EncodeTyped(ops, "direct", DirectPoolAlias.Codec, direct),
            RandomPoolAlias random => EncodeTyped(ops, "random", RandomPoolAlias.Codec, random),
            RandomGroupPoolAlias group => EncodeTyped(ops, "random_group", RandomGroupPoolAlias.Codec, group),
            _ => DataResult<U>.Error(() => $"unsupported pool alias {value.GetType().Name}"),
        };

    //EncodeTyped encodes by concrete type and adds the type field
    private static DataResult<U> EncodeTyped<U, T>(DynamicOps<U> ops, string type, MapCodec<T> codec, T value)
        where T : PoolAliasBinding
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace(type).ToString()));
        codec.EncodeTo<U>(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}
