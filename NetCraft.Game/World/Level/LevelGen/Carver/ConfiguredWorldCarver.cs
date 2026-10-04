using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Carver;

//ConfiguredWorldCarver 配置化雕刻器对应原版 ConfiguredWorldCarver
//把雕刻器与其配置绑成注册表元素 类型名由 type 字段派发 config 走各自配置 codec
public sealed class ConfiguredWorldCarver : NetCraft.Registry.ConfiguredWorldCarver
{
    //Codec 元素 codec 对应原版 DIRECT_CODEC
    public static readonly Codec<ConfiguredWorldCarver> Codec = new ConfiguredWorldCarverCodec();

    //ElementCodec 注册表元素 codec 注册表按标记接口持有元素
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

    //IsStartChunk 该区块是否要起一条雕刻对应原版 isStartChunk
    public bool IsStartChunk(RandomSource random) => WorldCarver.IsStartChunk(Config, random);

    //Carve 执行一次雕刻对应原版 carve
    public bool Carve(CarvingContext context, ChunkAccess chunk, Func<int, int, int, Biome> biomeGetter,
        RandomSource random, Aquifer aquifer, ChunkPos sourceChunkPos, CarvingMask mask)
        => WorldCarver.Carve(context, Config, chunk, biomeGetter, random, aquifer, sourceChunkPos, mask);

    public override string ToString() => $"{WorldCarver.Id}[{Config}]";
}

//ConfiguredWorldCarverCodec 按 type 字段查 CARVER 注册表再解 config 对应原版 dispatch codec
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

//CarvingContext 雕刻上下文对应原版 CarvingContext
//除生成范围外还带上 NoiseChunk 与含水层 让雕刻能问含水层该格是空气还是流体
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

    //TopMaterial 雕刻挖穿草方块后重算下方顶面材质对应原版 topMaterial
    public BlockState? TopMaterial(Func<int, int, int, Biome> biomeGetter, ChunkAccess chunk, int x, int y, int z,
        bool underFluid)
    {
        if (_surfaceRule is null) return null;
        return RandomState.SurfaceSystem.TopMaterial(_surfaceRule, chunk, _noiseChunk, biomeGetter, GetMinGenY(),
            GetGenDepth(), x, y, z, underFluid);
    }
}
