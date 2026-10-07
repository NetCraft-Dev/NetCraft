using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureCodecs structure codec entry point, maps to vanilla Structure.DIRECT_CODEC
//Common settings fields come from ReadSettings; each concrete structure type appends its own type-specific fields in its codec
public static class StructureCodecs
{
    //ElementCodec structure registry element codec, dispatches by type to the structure type registered in STRUCTURE_TYPE
    public static readonly Codec<NetCraft.Registry.Structure> ElementCodec = new StructureDispatchCodec();

    //ReadSettings parses the common structure settings fields biomes / step / terrain_adaptation, reused by each type's codec
    //spawn_overrides is ignored during parsing since this project does not implement mob spawn overrides
    internal static DataResult<StructureGenerationSettings> ReadSettings<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var biomesTag = input.Get("biomes");
        if (!biomesTag.IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "structure is missing required field biomes");
        var biomes = HolderSetCodecs.BiomeSet.Parse(ops, biomesTag.Get());
        if (!biomes.Result().IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "failed to parse structure biomes");

        var step = Features.GenerationStep.Decoration.SurfaceStructures;
        var stepTag = input.Get("step");
        if (stepTag.IsPresent)
        {
            var text = ops.GetStringValue(stepTag.Get());
            if (!text.Result().IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "step must be a string");
            var parsed = Features.GenerationStep.TryParse(text.GetOrThrow());
            if (parsed is null) return DataResult<StructureGenerationSettings>.Error(() => $"unknown decoration step: {text.GetOrThrow()}");
            step = parsed.Value;
        }

        var adaptation = TerrainAdjustment.None;
        var adaptationTag = input.Get("terrain_adaptation");
        if (adaptationTag.IsPresent)
        {
            var text = ops.GetStringValue(adaptationTag.Get());
            if (!text.Result().IsPresent) return DataResult<StructureGenerationSettings>.Error(() => "terrain_adaptation must be a string");
            var parsed = TerrainAdjustments.TryParse(text.GetOrThrow());
            if (parsed is null) return DataResult<StructureGenerationSettings>.Error(() => $"unknown terrain adaptation: {text.GetOrThrow()}");
            adaptation = parsed.Value;
        }

        return DataResult<StructureGenerationSettings>.Success(new StructureGenerationSettings(biomes.GetOrThrow(), step, adaptation));
    }
}

//StructureDispatchCodec looks up STRUCTURE_TYPE by the type field and hands off to that type's codec, maps to vanilla dispatch codec
internal sealed class StructureDispatchCodec : ScalarCodec<NetCraft.Registry.Structure>
{
    public override DataResult<NetCraft.Registry.Structure> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "structure must be an object");
        var map = mapResult.GetOrThrow();
        var typeTag = map.Get("type");
        if (!typeTag.IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "structure is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "structure type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null) return DataResult<NetCraft.Registry.Structure>.Error(() => $"invalid structure type: {typeText.GetOrThrow()}");
        var type = BuiltInRegistries.STRUCTURE_TYPE.GetValue(typeId.Value) as StructureType;
        if (type is null) return DataResult<NetCraft.Registry.Structure>.Error(() => $"unregistered structure type: {typeId}");
        if (type.ElementCodec is null) return DataResult<NetCraft.Registry.Structure>.Error(() => $"structure type {typeId} does not support data-driven loading");
        return type.ElementCodec.Parse(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.Structure value)
        => value is Structure structure && structure.Type.ElementCodec is { } codec
            ? codec.EncodeStart(ops, value)
            : DataResult<U>.Error(() => "structure has no element codec available, cannot encode");
}
