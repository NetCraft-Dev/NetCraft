using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseGeneratorSettings 噪声生成器配置对应原版 net.minecraft.world.level.levelgen.NoiseGeneratorSettings
//持有 NoiseSettings/NoiseRouter/SurfaceRules/spawnTarget/seaLevel 等参数
//DefaultBlock/DefaultFluid 为 BlockState 由 Game 层通过 Blocks.STONE.DefaultBlockState 注入内核不引用 Blocks
public sealed class NoiseGeneratorSettings
{
    public NoiseSettings NoiseSettings { get; }
    public BlockState DefaultBlock { get; }
    public BlockState DefaultFluid { get; }
    public NoiseRouter NoiseRouter { get; }
    public int SeaLevel { get; }
    public bool DisableMobGeneration { get; }
    public bool AquifersEnabled { get; }
    public bool OreVeinsEnabled { get; }
    public bool UseLegacyRandomSource { get; }
    public int BedrockRoofPosition { get; }
    public int BedrockFloorPosition { get; }

    //SurfaceRule 表面规则树 JSON surface_rule 字段
    public SurfaceRules.RuleSource? SurfaceRule { get; }

    //SpawnTarget 出生点气候目标 JSON spawn_target 字段
    public IReadOnlyList<Climate.ParameterPoint> SpawnTarget { get; }

    public NoiseGeneratorSettings(
        NoiseSettings noiseSettings,
        BlockState defaultBlock,
        BlockState defaultFluid,
        NoiseRouter noiseRouter,
        int seaLevel,
        bool disableMobGeneration,
        bool aquifersEnabled,
        bool oreVeinsEnabled,
        bool useLegacyRandomSource,
        int bedrockRoofPosition = -10,
        int bedrockFloorPosition = -10,
        SurfaceRules.RuleSource? surfaceRule = null,
        IReadOnlyList<Climate.ParameterPoint>? spawnTarget = null)
    {
        NoiseSettings = noiseSettings;
        DefaultBlock = defaultBlock;
        DefaultFluid = defaultFluid;
        NoiseRouter = noiseRouter;
        SeaLevel = seaLevel;
        DisableMobGeneration = disableMobGeneration;
        AquifersEnabled = aquifersEnabled;
        OreVeinsEnabled = oreVeinsEnabled;
        UseLegacyRandomSource = useLegacyRandomSource;
        BedrockRoofPosition = bedrockRoofPosition;
        BedrockFloorPosition = bedrockFloorPosition;
        SurfaceRule = surfaceRule;
        SpawnTarget = spawnTarget ?? Array.Empty<Climate.ParameterPoint>();
    }

    //LegacyCtor 兼容旧 7 参数构造函数对应旧简化版签名
    //旧测试用此构造函数 NoiseSettings 默认 OverworlddefaultBlock/defaultFluid 用 AIR 占位useLegacyRandomSource=false
    //新代码应使用完整 9+ 参数构造函数或 Overworld()/Nether() 等工厂方法
    public NoiseGeneratorSettings(
        NoiseRouter noiseRouter,
        int seaLevel,
        bool disableMobGeneration,
        bool aquifersEnabled,
        bool oreVeinsEnabled,
        int bedrockRoofPosition = -10,
        int bedrockFloorPosition = -10)
        : this(NoiseSettings.Overworld,
            Blocks.AIR.DefaultBlockState,
            Blocks.AIR.DefaultBlockState,
            noiseRouter,
            seaLevel,
            disableMobGeneration,
            aquifersEnabled,
            oreVeinsEnabled,
            useLegacyRandomSource: false,
            bedrockRoofPosition,
            bedrockFloorPosition)
    {
    }

    //Codec 11 字段完整编解码对应原版 NoiseGeneratorSettings.DIRECT_CODEC
    //覆盖真实 overworld.json 顶层全部字段 noise/default_block/default_fluid/noise_router/surface_rule/spawn_target 等
    //bedrock_roof_position/bedrock_floor_position 不在 JSON 中保持构造默认值
    public static readonly Codec<NoiseGeneratorSettings> Codec =
        RecordCodecBuilder.Of11(
            Codecs.Bool.FieldOf("aquifers_enabled").ForGetter<NoiseGeneratorSettings, bool>(s => s.AquifersEnabled),
            BlockStateCodec.Instance.FieldOf("default_block").ForGetter<NoiseGeneratorSettings, BlockState>(s => s.DefaultBlock),
            BlockStateCodec.Instance.FieldOf("default_fluid").ForGetter<NoiseGeneratorSettings, BlockState>(s => s.DefaultFluid),
            Codecs.Bool.FieldOf("disable_mob_generation").ForGetter<NoiseGeneratorSettings, bool>(s => s.DisableMobGeneration),
            Codecs.Bool.FieldOf("legacy_random_source").ForGetter<NoiseGeneratorSettings, bool>(s => s.UseLegacyRandomSource),
            NoiseSettings.Codec.FieldOf("noise").ForGetter<NoiseGeneratorSettings, NoiseSettings>(s => s.NoiseSettings),
            NoiseRouterCodec.Instance.FieldOf("noise_router").ForGetter<NoiseGeneratorSettings, NoiseRouter>(s => s.NoiseRouter),
            Codecs.Bool.FieldOf("ore_veins_enabled").ForGetter<NoiseGeneratorSettings, bool>(s => s.OreVeinsEnabled),
            Codecs.Int.FieldOf("sea_level").ForGetter<NoiseGeneratorSettings, int>(s => s.SeaLevel),
            Climate.ParameterPointCodec.ListOf().FieldOf("spawn_target")
                .ForGetter<NoiseGeneratorSettings, IReadOnlyList<Climate.ParameterPoint>>(s => s.SpawnTarget),
            SurfaceRulesCodecs.RuleSourceCodec.FieldOf("surface_rule")
                .ForGetter<NoiseGeneratorSettings, SurfaceRules.RuleSource?>(s => s.SurfaceRule),
            (aquifers, defaultBlock, defaultFluid, disableMob, legacy, noise, router, oreVeins, seaLevel, spawnTarget, surfaceRule)
                => new NoiseGeneratorSettings(noise, defaultBlock, defaultFluid, router, seaLevel, disableMob,
                    aquifers, oreVeins, legacy, surfaceRule: surfaceRule, spawnTarget: spawnTarget));

    //Overworld 主世界配置对应原版 NoiseGeneratorSettings.overworld
    //Game 层注入 Blocks.STONE/WATER 的 DefaultBlockState确保 Bootstrap 后调用
    public static NoiseGeneratorSettings Overworld()
        => new(
            NoiseSettings.Overworld,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            NoiseRouterData.Overworld(BuiltInRegistries.NOISE, false, false),
            seaLevel: 63,
            disableMobGeneration: false,
            aquifersEnabled: true,
            oreVeinsEnabled: true,
            useLegacyRandomSource: false,
            //出生点气候搜索依赖这张表 兜底配置也要带上 否则无数据包时搜索整条链路不生效
            spawnTarget: new OverworldBiomeBuilder().SpawnTarget());

    //Nether 下界配置对应原版 nether
    public static NoiseGeneratorSettings Nether()
        => new(
            NoiseSettings.Nether,
            Blocks.STONE.DefaultBlockState,
            Blocks.LAVA.DefaultBlockState,
            NoiseRouterData.Nether(BuiltInRegistries.NOISE),
            seaLevel: 32,
            disableMobGeneration: false,
            aquifersEnabled: false,
            oreVeinsEnabled: false,
            useLegacyRandomSource: true);

    //End 末地配置对应原版 end
    public static NoiseGeneratorSettings End()
        => new(
            NoiseSettings.End,
            Blocks.STONE.DefaultBlockState,
            Blocks.AIR.DefaultBlockState,
            NoiseRouterData.End(BuiltInRegistries.NOISE),
            seaLevel: 0,
            disableMobGeneration: true,
            aquifersEnabled: false,
            oreVeinsEnabled: false,
            useLegacyRandomSource: true);

    //Caves 洞穴维度配置对应原版 caves
    public static NoiseGeneratorSettings Caves()
        => new(
            NoiseSettings.Caves,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            NoiseRouterData.Caves(BuiltInRegistries.NOISE),
            seaLevel: 32,
            disableMobGeneration: false,
            aquifersEnabled: false,
            oreVeinsEnabled: false,
            useLegacyRandomSource: true);

    //FloatingIslands 浮空岛配置对应原版 floatingIslands
    public static NoiseGeneratorSettings FloatingIslands()
        => new(
            NoiseSettings.FloatingIslands,
            Blocks.STONE.DefaultBlockState,
            Blocks.WATER.DefaultBlockState,
            NoiseRouterData.FloatingIslands(BuiltInRegistries.NOISE),
            seaLevel: -64,
            disableMobGeneration: false,
            aquifersEnabled: false,
            oreVeinsEnabled: false,
            useLegacyRandomSource: true);

    //Dummy 测试用空配置对应原版 dummy
    public static NoiseGeneratorSettings Dummy()
        => new(
            NoiseSettings.Overworld,
            Blocks.STONE.DefaultBlockState,
            Blocks.AIR.DefaultBlockState,
            NoiseRouterData.None(),
            seaLevel: 63,
            disableMobGeneration: true,
            aquifersEnabled: false,
            oreVeinsEnabled: false,
            useLegacyRandomSource: false);
}
