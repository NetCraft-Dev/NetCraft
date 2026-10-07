using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//BiomeSourceCodecs biome source codecs, maps to vanilla BiomeSources registration and dispatch
//Vanilla dispatches through the MapCodec registry; this codebase has a closed set of types, so branch on the type field by hand
//multi_noise supports the preset form; the_end pulls its five end biomes from the registry
public static class BiomeSourceCodecs
{
    //Decode derive the biome source from the type field
    public static DataResult<BiomeSource> Decode<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeMap(ops, map));

    private static DataResult<BiomeSource> DecodeMap<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeText = ReadString(ops, input, "type");
        if (typeText is null)
            return DataResult<BiomeSource>.Error(() => "biome source is missing the type field");
        var typeId = Identifier.TryParse(typeText);
        if (typeId is null)
            return DataResult<BiomeSource>.Error(() => $"invalid biome source type id {typeText}");

        switch (typeId.Value.Path)
        {
            case "multi_noise":
                return DecodeMultiNoise(ops, input);
            case "the_end":
                return DecodeTheEnd(ops);
            default:
                return DataResult<BiomeSource>.Error(() => $"unsupported biome source type {typeId}");
        }
    }

    //DecodeTheEnd the end biome source takes no arguments; its five fixed biomes come from the registry
    private static DataResult<BiomeSource> DecodeTheEnd<U>(DynamicOps<U> ops)
    {
        var source = TheEndBiomeSource.FromRegistry(BiomeRegistry(ops));
        return source is null
            ? DataResult<BiomeSource>.Error(() => "end biome source is missing required biomes (the_end/end_highlands/end_midlands/small_end_islands/end_barrens)")
            : DataResult<BiomeSource>.Success(source);
    }

    //DecodeMultiNoise multi-noise biome source; only the preset form is supported for now
    //The inline biomes list form is unused by the data pack; add it when needed
    private static DataResult<BiomeSource> DecodeMultiNoise<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var presetText = ReadString(ops, input, "preset");
        if (presetText is null)
            return DataResult<BiomeSource>.Error(() => "multi_noise only supports the preset form for now");
        var presetId = Identifier.TryParse(presetText);
        if (presetId is null)
            return DataResult<BiomeSource>.Error(() => $"invalid parameter list preset id {presetText}");
        //The parameter list must already be loaded by data; if missing, the pack lacks worldgen/multi_noise_biome_source_parameter_list
        if (ParameterListRegistry(ops)?.GetValue(presetId.Value) is not MultiNoiseBiomeSourceParameterList parameterList)
            return DataResult<BiomeSource>.Error(() => $"parameter list preset {presetId} not found");
        return DataResult<BiomeSource>.Success(new MultiNoiseBiomeSource(parameterList));
    }

    //ParameterListRegistry prefer the registry carried by RegistryOps; fall back to the built-in one for plain ops
    private static Registry<object>? ParameterListRegistry<U>(DynamicOps<U> ops)
        => (ops as RegistryOps<U>)?.GetRegistry(Registries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST)
            ?? BuiltInRegistries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST;

    //BiomeRegistry same as above for biomes; tests can pass an isolated registry without touching the global one
    private static Registry<Biome>? BiomeRegistry<U>(DynamicOps<U> ops)
        => (ops as RegistryOps<U>)?.GetRegistry(Registries.BIOME) ?? BuiltInRegistries.BIOME;

    //ReadString read a string field; returns null when missing or not a string
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }
}
