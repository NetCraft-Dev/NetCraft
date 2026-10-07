using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseGeneratorSettings noise generator settings, maps to vanilla net.minecraft.world.level.levelgen.NoiseGeneratorSettings
//Holds NoiseSettings/NoiseRouter/SurfaceRules/spawnTarget/seaLevel and other parameters
//DefaultBlock/DefaultFluid are BlockStates injected by the Game layer through Blocks.STONE.DefaultBlockState; the core does not reference Blocks
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

    //SurfaceRule surface rule tree, the JSON surface_rule field
    public SurfaceRules.RuleSource? SurfaceRule { get; }

    //SpawnTarget spawn point climate target, the JSON spawn_target field
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

    //LegacyCtor keeps the old 7-argument constructor, matching the old simplified signature
    //Old tests use this constructor; NoiseSettings defaults to Overworld, defaultBlock/defaultFluid use AIR as a placeholder and useLegacyRandomSource is false
    //New code should use the full 9+ argument constructor or factory methods such as Overworld()/Nether()
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

    //Codec full 11-field codec, maps to vanilla NoiseGeneratorSettings.DIRECT_CODEC
    //Covers every top-level field of a real overworld.json: noise/default_block/default_fluid/noise_router/surface_rule/spawn_target and more
    //bedrock_roof_position/bedrock_floor_position are not in the JSON and keep the constructor defaults
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

    //Overworld overworld settings, maps to vanilla NoiseGeneratorSettings.overworld
    //The Game layer injects Blocks.STONE/WATER DefaultBlockState; make sure to call after Bootstrap
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
            //Spawn point climate search depends on this table; the fallback config must carry it too or the whole search chain does nothing without a data pack
            spawnTarget: new OverworldBiomeBuilder().SpawnTarget());

    //Nether the Nether settings, maps to vanilla nether
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

    //End the End settings, maps to vanilla end
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

    //Caves the caves dimension settings, maps to vanilla caves
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

    //FloatingIslands floating islands settings, maps to vanilla floatingIslands
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

    //Dummy empty settings for tests, maps to vanilla dummy
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
