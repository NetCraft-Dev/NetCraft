using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//LevelStem level stem, maps to vanilla net.minecraft.world.level.dimension.LevelStem
//A dimension is a dimension type reference plus a chunk generator; a world_preset in the data pack is a set of LevelStem
//TypeId is a key in the dimension_type registry; assembly looks up the real DimensionType by it
public sealed record LevelStem(Identifier TypeId, ChunkGenerator Generator) : NetCraft.Registry.LevelStem
{
    //Codec element codec, maps to vanilla LevelStem.CODEC
    public static readonly Codec<LevelStem> Codec = new LevelStemCodec();
}

//LevelStemCodec level stem codec, maps to vanilla LevelStem.CODEC
//The generator type is currently only minecraft:noise in the data pack, so branch inline instead of dispatching through the registry
internal sealed class LevelStemCodec : AbstractMapCodec<LevelStem>
{
    public override DataResult<LevelStem> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeText = ReadString(ops, input, "type");
        if (typeText is null)
            return DataResult<LevelStem>.Error(() => "level stem is missing the type field");
        var typeId = Identifier.TryParse(typeText);
        if (typeId is null)
            return DataResult<LevelStem>.Error(() => $"invalid dimension type id {typeText}");
        var generatorTag = input.Get("generator");
        if (!generatorTag.IsPresent)
            return DataResult<LevelStem>.Error(() => "level stem is missing the generator field");
        var generator = DecodeGenerator(ops, generatorTag.Get());
        if (!generator.Result().IsPresent)
            return DataResult<LevelStem>.Error(() => "failed to parse generator");
        return DataResult<LevelStem>.Success(new LevelStem(typeId.Value, generator.GetOrThrow()));
    }

    //EncodeTo there is no need to write back level stems at this stage; dimensions in level.dat are written as an empty map
    //Add it when write-back is needed; the noise settings registry name can be looked up from NOISE_SETTINGS
    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, LevelStem value, RecordBuilder<U> builder)
        => throw new NotSupportedException("encoding level stems is not supported yet");

    private static DataResult<ChunkGenerator> DecodeGenerator<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeGeneratorMap(ops, map));

    private static DataResult<ChunkGenerator> DecodeGeneratorMap<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeText = ReadString(ops, input, "type");
        if (typeText is null)
            return DataResult<ChunkGenerator>.Error(() => "chunk generator is missing the type field");
        var typeId = Identifier.TryParse(typeText);
        if (typeId is null)
            return DataResult<ChunkGenerator>.Error(() => $"invalid chunk generator type id {typeText}");
        if (typeId.Value.Path != "noise")
            return DataResult<ChunkGenerator>.Error(() => $"unsupported chunk generator type {typeId}");

        var biomeTag = input.Get("biome_source");
        if (!biomeTag.IsPresent)
            return DataResult<ChunkGenerator>.Error(() => "noise generator is missing the biome_source field");
        var biomeSource = BiomeSourceCodecs.Decode(ops, biomeTag.Get());
        if (!biomeSource.Result().IsPresent)
            return DataResult<ChunkGenerator>.Error(() => "failed to parse biome_source");

        var settingsText = ReadString(ops, input, "settings");
        if (settingsText is null)
            return DataResult<ChunkGenerator>.Error(() => "noise generator is missing the settings field");
        var settingsId = Identifier.TryParse(settingsText);
        if (settingsId is null)
            return DataResult<ChunkGenerator>.Error(() => $"invalid noise settings id {settingsText}");
        //Noise settings are loaded by data at step 8; if missing, the pack lacks worldgen/noise_settings
        var settingsRegistry = (ops as RegistryOps<U>)?.GetRegistry(Registries.NOISE_SETTINGS)
            ?? BuiltInRegistries.NOISE_SETTINGS;
        if (settingsRegistry.GetValue(settingsId.Value) is not NoiseGeneratorSettings settings)
            return DataResult<ChunkGenerator>.Error(() => $"noise settings {settingsId} not found");
        return DataResult<ChunkGenerator>.Success(new NoiseBasedChunkGenerator(biomeSource.GetOrThrow(), settings));
    }

    //ReadString read a string field; returns null when missing or not a string
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }
}
