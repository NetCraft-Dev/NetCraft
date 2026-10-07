using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePlacementType structure placement type base class, maps to vanilla StructurePlacementType
//Holds the registry name and decode entry; loading dispatches to a concrete type by the placement's type field
public abstract class StructurePlacementType : NetCraft.Registry.StructurePlacementType<object>
{
    public Identifier Id { get; }

    protected StructurePlacementType(Identifier id) => Id = id;

    //Decode decodes a placement instance from a map; the type field is already consumed by the caller
    public abstract DataResult<StructurePlacement> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //Register registers into STRUCTURE_PLACEMENT and returns itself so a static field can be assigned directly
    protected static T Register<T>(Identifier id, T type) where T : StructurePlacementType
    {
        Registry<NetCraft.Registry.StructurePlacementType<object>>.Register(
            BuiltInRegistries.STRUCTURE_PLACEMENT, id, type);
        return type;
    }

    public override string ToString() => $"StructurePlacementType[{Id}]";
}

//RandomSpreadStructurePlacementType random spread placement type, maps to vanilla StructurePlacementType.RANDOM_SPREAD
public sealed class RandomSpreadStructurePlacementType : StructurePlacementType
{
    public static readonly RandomSpreadStructurePlacementType Instance =
        Register(Identifier.WithDefaultNamespace("random_spread"), new RandomSpreadStructurePlacementType());

    private RandomSpreadStructurePlacementType()
        : base(Identifier.WithDefaultNamespace("random_spread")) { }

    //Decode parses the common fields and grid params, maps to vanilla RandomSpreadStructurePlacement.CODEC
    public override DataResult<StructurePlacement> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => StructurePlacementCodecs.ReadCommon(ops, input).FlatMap(parts =>
        {
            var spacing = StructurePlacementCodecs.ReadIntField(ops, input, "spacing");
            if (!spacing.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "random_spread is missing spacing");
            var separation = StructurePlacementCodecs.ReadIntField(ops, input, "separation");
            if (!separation.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "random_spread is missing separation");
            var spreadType = RandomSpreadType.Linear;
            var spreadTag = input.Get("spread_type");
            if (spreadTag.IsPresent)
            {
                var text = ops.GetStringValue(spreadTag.Get());
                if (!text.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "spread_type must be a string");
                var parsed = RandomSpreadTypes.TryParse(text.GetOrThrow());
                if (parsed is null) return DataResult<StructurePlacement>.Error(() => $"unknown spread_type: {text.GetOrThrow()}");
                spreadType = parsed.Value;
            }
            return DataResult<StructurePlacement>.Success(new RandomSpreadStructurePlacement(
                parts.LocateOffset, parts.ReductionMethod, parts.Frequency, parts.Salt, parts.Exclusion,
                spacing.GetOrThrow(), separation.GetOrThrow(), spreadType));
        });
}

//StructurePlacementCodecs structure placement codec entry point, maps to vanilla StructurePlacement.CODEC
public static class StructurePlacementCodecs
{
    //ElementCodec registry element codec, dispatches by type to the placement type registered in STRUCTURE_PLACEMENT
    public static readonly Codec<StructurePlacement> ElementCodec = new PlacementDispatchCodec();

    //ReadCommon parses the common placement fields, reused by each placement type
    //locate_offset and frequency_reduction_method are optional, frequency defaults to 1.0, salt is required
    internal static DataResult<PlacementParts> ReadCommon<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var locateOffset = Vec3i.Zero;
        var offsetTag = input.Get("locate_offset");
        if (offsetTag.IsPresent)
        {
            var offset = ReadVec3i(ops, offsetTag.Get());
            if (!offset.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "locate_offset must be a triple of signed integers");
            locateOffset = offset.GetOrThrow();
        }

        var method = FrequencyReductionMethod.Default;
        var methodTag = input.Get("frequency_reduction_method");
        if (methodTag.IsPresent)
        {
            var text = ops.GetStringValue(methodTag.Get());
            if (!text.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "frequency_reduction_method must be a string");
            var parsed = FrequencyReductionMethods.TryParse(text.GetOrThrow());
            if (parsed is null) return DataResult<PlacementParts>.Error(() => $"unknown frequency_reduction_method: {text.GetOrThrow()}");
            method = parsed.Value;
        }

        var frequency = 1.0f;
        var frequencyTag = input.Get("frequency");
        if (frequencyTag.IsPresent)
        {
            var value = ops.GetNumberValue(frequencyTag.Get());
            if (!value.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "frequency must be a number");
            frequency = (float)value.GetOrThrow();
        }

        var saltTag = input.Get("salt");
        if (!saltTag.IsPresent) return DataResult<PlacementParts>.Error(() => "placement is missing required field salt");
        var saltValue = ops.GetNumberValue(saltTag.Get());
        if (!saltValue.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "salt must be an integer");
        var salt = (int)saltValue.GetOrThrow();

        StructurePlacement.ExclusionZone? exclusion = null;
        var exclusionTag = input.Get("exclusion_zone");
        if (exclusionTag.IsPresent)
        {
            var mapResult = ops.GetMap(exclusionTag.Get());
            if (!mapResult.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone must be an object");
            var map = mapResult.GetOrThrow();
            var otherSetTag = map.Get("other_set");
            if (!otherSetTag.IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone is missing other_set");
            var otherSet = HolderSetCodecs.StructureSetRef.Parse(ops, otherSetTag.Get());
            if (!otherSet.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "failed to parse exclusion_zone.other_set");
            var chunkCount = ReadIntField(ops, map, "chunk_count");
            if (!chunkCount.Result().IsPresent) return DataResult<PlacementParts>.Error(() => "exclusion_zone.chunk_count must be an integer");
            exclusion = new StructurePlacement.ExclusionZone(otherSet.GetOrThrow(), chunkCount.GetOrThrow());
        }

        return DataResult<PlacementParts>.Success(new PlacementParts(locateOffset, method, frequency, salt, exclusion));
    }

    //ReadIntField reads an integer scalar field, erroring on a missing or non-numeric value
    internal static DataResult<int> ReadIntField<U>(DynamicOps<U> ops, MapLike<U> map, string key)
    {
        var tag = map.Get(key);
        if (!tag.IsPresent) return DataResult<int>.Error(() => $"missing field {key}");
        var value = ops.GetNumberValue(tag.Get());
        if (!value.Result().IsPresent) return DataResult<int>.Error(() => $"{key} must be a number");
        return DataResult<int>.Success((int)value.GetOrThrow());
    }

    //ReadVec3i reads a three-element integer array, maps to the [x, y, z] form of vanilla Vec3i.CODEC
    internal static DataResult<Vec3i> ReadVec3i<U>(DynamicOps<U> ops, U input)
    {
        var streamResult = ops.GetStream(input);
        if (!streamResult.Result().IsPresent)
            return DataResult<Vec3i>.Error(() => "coordinates must be a triple of signed integers");
        var values = new List<int>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var number = ops.GetNumberValue(element);
            if (!number.Result().IsPresent)
                return DataResult<Vec3i>.Error(() => "coordinates must be a triple of signed integers");
            var value = number.GetOrThrow();
            if (value != Math.Floor(value))
                return DataResult<Vec3i>.Error(() => "coordinates must be a triple of signed integers");
            values.Add((int)value);
        }
        if (values.Count != 3)
            return DataResult<Vec3i>.Error(() => $"coordinates must be a triple of signed integers, got {values.Count}");
        return DataResult<Vec3i>.Success(new Vec3i(values[0], values[1], values[2]));
    }
}

//PlacementParts parse result of the common placement fields, used by each placement type to build its own instance
internal sealed record PlacementParts(Vec3i LocateOffset, FrequencyReductionMethod ReductionMethod,
    float Frequency, int Salt, StructurePlacement.ExclusionZone? Exclusion);

//PlacementDispatchCodec looks up STRUCTURE_PLACEMENT by the type field then hands off to that type's decode, maps to vanilla dispatch codec
internal sealed class PlacementDispatchCodec : ScalarCodec<StructurePlacement>
{
    public override DataResult<StructurePlacement> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "placement must be an object");
        var map = mapResult.GetOrThrow();
        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return DataResult<StructurePlacement>.Error(() => "placement is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<StructurePlacement>.Error(() => "placement.type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<StructurePlacement>.Error(() => $"invalid placement type: {typeText.GetOrThrow()}");
        var type = BuiltInRegistries.STRUCTURE_PLACEMENT.GetValue(typeId.Value) as StructurePlacementType;
        if (type is null) return DataResult<StructurePlacement>.Error(() => $"unregistered placement type: {typeId}");
        return type.Decode(ops, map);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructurePlacement value)
        => DataResult<U>.Error(() => "structure placement encoding not implemented yet");
}
