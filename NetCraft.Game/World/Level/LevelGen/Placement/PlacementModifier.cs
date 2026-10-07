using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacementContext placement context, maps to vanilla PlacementContext
//Carries the world and generator in addition to the generation range; modifiers use it to query heightmaps, blocks and biomes
public sealed class PlacementContext : WorldGenerationContext
{
    public WorldGenRegion Level { get; }
    public ChunkGenerator Generator { get; }

    //TopFeature the placed feature currently being placed; the biome filter uses it to look up the owning biomes
    public PlacedFeature? TopFeature { get; }

    public PlacementContext(WorldGenRegion level, ChunkGenerator generator, PlacedFeature? topFeature)
        : base(generator, level)
    {
        Level = level;
        Generator = generator;
        TopFeature = topFeature;
    }
}

//PlacementModifierType placement modifier type singleton, maps to vanilla PlacementModifierType
//Holds the type id and the map-to-instance decode entry, registered into the PLACEMENT_MODIFIER_TYPE registry
public abstract class PlacementModifierType : NetCraft.Registry.PlacementModifierType
{
    public Identifier Id { get; }

    protected PlacementModifierType(Identifier id) => Id = id;

    //Decode decode a parameterized modifier instance from the map; the type field is already consumed by the caller
    public abstract DataResult<PlacementModifier> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields accumulate the instance fields into the builder; the type field is added by the caller
    public abstract void EncodeFields<U>(DynamicOps<U> ops, PlacementModifier value, RecordBuilder<U> builder);

    //Register register into PLACEMENT_MODIFIER_TYPE and return itself, so static fields can assign it directly
    protected static T Register<T>(Identifier id, T type) where T : PlacementModifierType
    {
        Registry<NetCraft.Registry.PlacementModifierType>.Register(
            BuiltInRegistries.PLACEMENT_MODIFIER_TYPE, id, type);
        return type;
    }
}

//PlacementModifierType<P> generic middle layer for a concrete modifier type; subclasses only supply one MapCodec<P>
public abstract class PlacementModifierType<P> : PlacementModifierType where P : PlacementModifier
{
    private readonly MapCodec<P> _codec;

    protected PlacementModifierType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<PlacementModifier> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (PlacementModifier)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, PlacementModifier value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

//PlacementModifier placement modifier instance base, maps to vanilla PlacementModifier
//Derives the next batch of positions from the current one; chaining several modifiers forms the placement pipeline
public abstract class PlacementModifier
{
    //Type owning type singleton; encoding and logging use it to get the id
    public abstract PlacementModifierType Type { get; }

    //GetPositions derive a batch of positions from the origin, maps to vanilla getPositions
    public abstract IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin);
}

//PlacementModifierCodec look up PLACEMENT_MODIFIER_TYPE by the type field then delegate decoding to that type
//Maps to vanilla BuiltInRegistries.PLACEMENT_MODIFIER_TYPE.byNameCodec().dispatch(...)
//type and its parameters are flattened onto the same level, matching vanilla inlining the MapCodec fields into the dispatch result
internal sealed class PlacementModifierCodec : ScalarCodec<PlacementModifier>
{
    public static readonly PlacementModifierCodec Instance = new();

    public override DataResult<PlacementModifier> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeModifier(ops, map));

    private static DataResult<PlacementModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<PlacementModifier>.Error(() => "placement modifier is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<PlacementModifier>.Error(() => "placement modifier type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<PlacementModifier>.Error(() => $"invalid modifier type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.PLACEMENT_MODIFIER_TYPE.GetValue(typeId.Value) is not PlacementModifierType type)
            return DataResult<PlacementModifier>.Error(() => $"unknown modifier type: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PlacementModifier value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}
