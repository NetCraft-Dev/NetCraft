using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Loot;

//NumberProvider a count source resolved once per loot roll, maps to vanilla NumberProvider
public interface NumberProvider
{
    float GetFloat(LootContext context);

    int GetInt(LootContext context) => (int)MathF.Round(GetFloat(context));

    //TypeId is the registry name the type field dispatches on
    Identifier TypeId { get; }

    MapCodec<NumberProvider> Codec();
}

//ConstantValue a fixed count, maps to vanilla ConstantValue
public sealed record ConstantValue(float Value) : NumberProvider
{
    public static readonly MapCodec<NumberProvider> MAP_CODEC = RecordCodecBuilder.Of1<NumberProvider, float>(
        Codecs.Float.FieldOf("value").ForGetter<NumberProvider, float>(p => ((ConstantValue)p).Value),
        value => new ConstantValue(value));

    public Identifier TypeId => Identifier.WithDefaultNamespace("constant");

    public float GetFloat(LootContext context) => Value;

    public MapCodec<NumberProvider> Codec() => MAP_CODEC;

    public static ConstantValue Exactly(float value) => new(value);
}

//UniformGenerator a count drawn uniformly from a range, maps to vanilla UniformGenerator
public sealed record UniformGenerator(float Min, float Max) : NumberProvider
{
    public static readonly MapCodec<NumberProvider> MAP_CODEC = RecordCodecBuilder.Of2<NumberProvider, float, float>(
        Codecs.Float.FieldOf("min").ForGetter<NumberProvider, float>(p => ((UniformGenerator)p).Min),
        Codecs.Float.FieldOf("max").ForGetter<NumberProvider, float>(p => ((UniformGenerator)p).Max),
        (min, max) => new UniformGenerator(min, max));

    public Identifier TypeId => Identifier.WithDefaultNamespace("uniform");

    public float GetFloat(LootContext context) => context.Random.NextFloat() * (Max - Min) + Min;

    public MapCodec<NumberProvider> Codec() => MAP_CODEC;
}

//NumberProviders the number provider type registry plus the root codec, maps to vanilla NumberProviders
public static class NumberProviders
{
    private static readonly Dictionary<Identifier, MapCodec<NumberProvider>> Types = new();

    //TYPED_CODEC dispatches on the type field alone
    public static readonly Codec<NumberProvider> TYPED_CODEC =
        IdentifierCodec.Instance.Dispatch<NumberProvider, Identifier>("type", p => p.TypeId, Lookup);

    //CODEC also accepts a bare number, the shorthand vanilla writes for a constant count
    public static readonly Codec<NumberProvider> CODEC = new NumberProviderCodec();

    static NumberProviders()
    {
        Register("constant", ConstantValue.MAP_CODEC);
        Register("uniform", UniformGenerator.MAP_CODEC);
    }

    private static MapCodec<NumberProvider> Lookup(Identifier id)
        => Types.TryGetValue(id, out var codec)
            ? codec
            : throw new KeyNotFoundException($"Unknown number provider type: {id}");

    private static void Register(string name, MapCodec<NumberProvider> codec)
        => Types[Identifier.WithDefaultNamespace(name)] = codec;
}

//NumberProviderCodec reads a bare number as a constant and otherwise falls back to the typed form
internal sealed class NumberProviderCodec : ScalarCodec<NumberProvider>
{
    public override DataResult<NumberProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
            return DataResult<NumberProvider>.Success(ConstantValue.Exactly((float)number.GetOrThrow()));
        return NumberProviders.TYPED_CODEC.Parse(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NumberProvider value)
        => value is ConstantValue constant
            ? DataResult<U>.Success(ops.CreateFloat(constant.Value))
            : NumberProviders.TYPED_CODEC.EncodeStart(ops, value);
}
