using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Carver;

//ConfiguredWorldCarver configured carver, maps to vanilla ConfiguredWorldCarver
//Binds a carver to its config as a registry element; the type field dispatches, config goes through the per-type config codec
public sealed class ConfiguredWorldCarver : NetCraft.Registry.ConfiguredWorldCarver
{
    //Codec element codec, maps to vanilla DIRECT_CODEC
    public static readonly Codec<ConfiguredWorldCarver> Codec = new ConfiguredWorldCarverCodec();

    //ElementCodec registry element codec; the registry holds elements by marker interface
    public static readonly Codec<NetCraft.Registry.ConfiguredWorldCarver> ElementCodec = Codec.ComapFlatMap(
        carver => DataResult<NetCraft.Registry.ConfiguredWorldCarver>.Success(carver),
        carver => (ConfiguredWorldCarver)carver);

    public WorldCarver WorldCarver { get; }
    public CarverConfiguration Config { get; }

    public ConfiguredWorldCarver(WorldCarver worldCarver, CarverConfiguration config)
    {
        WorldCarver = worldCarver;
        Config = config;
    }

    //IsStartChunk whether this chunk should start a carve, maps to vanilla isStartChunk
    public bool IsStartChunk(RandomSource random) => WorldCarver.IsStartChunk(Config, random);

    //Carve run one carve, maps to vanilla carve
    public bool Carve(CarvingContext context, ChunkAccess chunk, Func<int, int, int, Biome> biomeGetter,
        RandomSource random, Aquifer aquifer, ChunkPos sourceChunkPos, CarvingMask mask)
        => WorldCarver.Carve(context, Config, chunk, biomeGetter, random, aquifer, sourceChunkPos, mask);

    public override string ToString() => $"{WorldCarver.Id}[{Config}]";
}

//ConfiguredWorldCarverCodec look up the CARVER registry by type field then decode config, maps to vanilla dispatch codec
internal sealed class ConfiguredWorldCarverCodec : ScalarCodec<ConfiguredWorldCarver>
{
    public override DataResult<ConfiguredWorldCarver> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeCarver(ops, map));

    private static DataResult<ConfiguredWorldCarver> DecodeCarver<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<ConfiguredWorldCarver>.Error(() => "missing type for carver");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<ConfiguredWorldCarver>.Error(() => "carver type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<ConfiguredWorldCarver>.Error(() => $"invalid carver type: {typeText.GetOrThrow()}");
        var carver = BuiltInRegistries.CARVER.GetValue(typeId.Value);
        if (carver is null)
            return DataResult<ConfiguredWorldCarver>.Error(() => $"unknown carver type: {typeId}");
        var configTag = input.Get("config");
        if (!configTag.IsPresent)
            return DataResult<ConfiguredWorldCarver>.Error(() => $"missing config for carver {typeId}");
        var worldCarver = (WorldCarver)carver;
        return worldCarver switch
        {
            CaveWorldCarver => CaveCarverConfiguration.Codec.Parse(ops, configTag.Get())
                .Map(config => new ConfiguredWorldCarver(worldCarver, config)),
            CanyonWorldCarver => CanyonCarverConfiguration.Codec.Parse(ops, configTag.Get())
                .Map(config => new ConfiguredWorldCarver(worldCarver, config)),
            _ => DataResult<ConfiguredWorldCarver>.Error(() => $"unsupported carver: {typeId}")
        };
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, ConfiguredWorldCarver value)
    {
        var configResult = value.Config switch
        {
            CanyonCarverConfiguration canyon => CanyonCarverConfiguration.Codec.EncodeStart(ops, canyon),
            CaveCarverConfiguration cave => CaveCarverConfiguration.Codec.EncodeStart(ops, cave),
            _ => DataResult<U>.Error(() => $"unsupported carver config: {value.Config.GetType().Name}")
        };
        if (!configResult.Result().IsPresent) return configResult;
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.WorldCarver.Id.ToString()));
        builder.Add("config", configResult.GetOrThrow());
        return builder.Build(ops.Empty());
    }
}

//CarvingContext carving context, maps to vanilla CarvingContext
//Carries the NoiseChunk and aquifer in addition to the generation range, so carving can ask the aquifer whether a cell is air or fluid
public sealed class CarvingContext : WorldGenerationContext
{
    private readonly NoiseChunk _noiseChunk;
    private readonly SurfaceRules.RuleSource? _surfaceRule;

    public RandomState RandomState { get; }

    public CarvingContext(NoiseBasedChunkGenerator generator, LevelHeightAccessor heightAccessor, NoiseChunk noiseChunk,
        RandomState randomState, SurfaceRules.RuleSource? surfaceRule)
        : base(generator, heightAccessor)
    {
        _noiseChunk = noiseChunk;
        RandomState = randomState;
        _surfaceRule = surfaceRule;
    }

    //TopMaterial recompute the top material below after carving through grass, maps to vanilla topMaterial
    public BlockState? TopMaterial(Func<int, int, int, Biome> biomeGetter, ChunkAccess chunk, int x, int y, int z,
        bool underFluid)
    {
        if (_surfaceRule is null) return null;
        return RandomState.SurfaceSystem.TopMaterial(_surfaceRule, chunk, _noiseChunk, biomeGetter, GetMinGenY(),
            GetGenDepth(), x, y, z, underFluid);
    }
}
