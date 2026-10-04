using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.Carver;

//CarverDebugSettings 雕刻调试设置对应原版 CarverDebugSettings
//开调试时把挖出来的空气/水/岩浆/屏障换成显眼方块 DEFAULT 对齐原版用按钮蜡烛与橙色玻璃
public sealed class CarverDebugSettings
{
    //DEFAULT 原版默认调试设置 方块表未注册时退回空气避免静态初始化抛异常
    public static readonly CarverDebugSettings DEFAULT = new(
        false,
        StateOf("acacia_button"),
        StateOf("candle"),
        StateOf("orange_stained_glass"),
        StateOf("glass"));

    public static readonly Codec<CarverDebugSettings> Codec =
        RecordCodecBuilder.Of5<CarverDebugSettings, bool, BlockState, BlockState, BlockState, BlockState>(
            Codecs.Bool.OptionalFieldOf("debug_mode", false).ForGetter<CarverDebugSettings, bool>(s => s.DebugMode),
            BlockStateCodec.Instance.OptionalFieldOf("air_state", DEFAULT.AirState)
                .ForGetter<CarverDebugSettings, BlockState>(s => s.AirState),
            BlockStateCodec.Instance.OptionalFieldOf("water_state", DEFAULT.AirState)
                .ForGetter<CarverDebugSettings, BlockState>(s => s.WaterState),
            BlockStateCodec.Instance.OptionalFieldOf("lava_state", DEFAULT.AirState)
                .ForGetter<CarverDebugSettings, BlockState>(s => s.LavaState),
            BlockStateCodec.Instance.OptionalFieldOf("barrier_state", DEFAULT.AirState)
                .ForGetter<CarverDebugSettings, BlockState>(s => s.BarrierState),
            (debugMode, airState, waterState, lavaState, barrierState) =>
                new CarverDebugSettings(debugMode, airState, waterState, lavaState, barrierState));

    public bool DebugMode { get; }
    public BlockState AirState { get; }
    public BlockState WaterState { get; }
    public BlockState LavaState { get; }
    public BlockState BarrierState { get; }

    public CarverDebugSettings(bool debugMode, BlockState airState, BlockState waterState, BlockState lavaState,
        BlockState barrierState)
    {
        DebugMode = debugMode;
        AirState = airState;
        WaterState = waterState;
        LavaState = lavaState;
        BarrierState = barrierState;
    }

    //StateOf 按注册名取方块默认状态 方块表没这个方块时退回空气
    private static BlockState StateOf(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path))?.DefaultBlockState
            ?? Blocks.AIR.DefaultBlockState;
}

//CarverConfiguration 雕刻配置基类对应原版 CarverConfiguration
//原版继承 ProbabilityFeatureConfiguration feature 体系尚未移植这里直接放 Probability 字段
public class CarverConfiguration
{
    public static readonly Codec<CarverConfiguration> Codec =
        RecordCodecBuilder.Of6<CarverConfiguration, float, HeightProvider, FloatProvider, VerticalAnchor, CarverDebugSettings, HolderSet<NetCraft.Registry.Block>>(
            Codecs.Float.FieldOf("probability").ForGetter<CarverConfiguration, float>(c => c.Probability),
            HeightProvider.Codec.FieldOf("y").ForGetter<CarverConfiguration, HeightProvider>(c => c.Y),
            FloatProviders.Codec.FieldOf("yScale").ForGetter<CarverConfiguration, FloatProvider>(c => c.YScale),
            VerticalAnchor.Codec.FieldOf("lava_level").ForGetter<CarverConfiguration, VerticalAnchor>(c => c.LavaLevel),
            CarverDebugSettings.Codec.OptionalFieldOf("debug_settings", CarverDebugSettings.DEFAULT)
                .ForGetter<CarverConfiguration, CarverDebugSettings>(c => c.DebugSettings),
            HolderSetCodecs.BlockSet.FieldOf("replaceable")
                .ForGetter<CarverConfiguration, HolderSet<NetCraft.Registry.Block>>(c => c.Replaceable),
            (probability, y, yScale, lavaLevel, debugSettings, replaceable) =>
                new CarverConfiguration(probability, y, yScale, lavaLevel, debugSettings, replaceable));

    public float Probability { get; }
    public HeightProvider Y { get; }
    public FloatProvider YScale { get; }
    public VerticalAnchor LavaLevel { get; }
    public CarverDebugSettings DebugSettings { get; }
    public HolderSet<NetCraft.Registry.Block> Replaceable { get; }

    public CarverConfiguration(float probability, HeightProvider y, FloatProvider yScale, VerticalAnchor lavaLevel,
        CarverDebugSettings debugSettings, HolderSet<NetCraft.Registry.Block> replaceable)
    {
        Probability = probability;
        Y = y;
        YScale = yScale;
        LavaLevel = lavaLevel;
        DebugSettings = debugSettings;
        Replaceable = replaceable;
    }
}

//CaveCarverConfiguration 洞穴雕刻配置对应原版 CaveCarverConfiguration
public sealed class CaveCarverConfiguration : CarverConfiguration
{
    public static readonly Codec<CaveCarverConfiguration> Codec =
        RecordCodecBuilder.Of4<CaveCarverConfiguration, CarverConfiguration, FloatProvider, FloatProvider, FloatProvider>(
            new FieldCodec<CaveCarverConfiguration, CarverConfiguration>(CarverConfiguration.Codec,
                c => (CarverConfiguration)c),
            FloatProviders.Codec.FieldOf("horizontal_radius_multiplier")
                .ForGetter<CaveCarverConfiguration, FloatProvider>(c => c.HorizontalRadiusMultiplier),
            FloatProviders.Codec.FieldOf("vertical_radius_multiplier")
                .ForGetter<CaveCarverConfiguration, FloatProvider>(c => c.VerticalRadiusMultiplier),
            FloatProviders.Codec.FieldOf("floor_level").ForGetter<CaveCarverConfiguration, FloatProvider>(c => c.FloorLevel),
            (baseConfig, horizontalRadiusMultiplier, verticalRadiusMultiplier, floorLevel) =>
                new CaveCarverConfiguration(baseConfig, horizontalRadiusMultiplier, verticalRadiusMultiplier, floorLevel));

    public FloatProvider HorizontalRadiusMultiplier { get; }
    public FloatProvider VerticalRadiusMultiplier { get; }
    public FloatProvider FloorLevel { get; }

    public CaveCarverConfiguration(CarverConfiguration baseConfig, FloatProvider horizontalRadiusMultiplier,
        FloatProvider verticalRadiusMultiplier, FloatProvider floorLevel)
        : base(baseConfig.Probability, baseConfig.Y, baseConfig.YScale, baseConfig.LavaLevel, baseConfig.DebugSettings,
            baseConfig.Replaceable)
    {
        HorizontalRadiusMultiplier = horizontalRadiusMultiplier;
        VerticalRadiusMultiplier = verticalRadiusMultiplier;
        FloorLevel = floorLevel;
    }

    public CaveCarverConfiguration(float probability, HeightProvider y, FloatProvider yScale, VerticalAnchor lavaLevel,
        HolderSet<NetCraft.Registry.Block> replaceable, FloatProvider horizontalRadiusMultiplier,
        FloatProvider verticalRadiusMultiplier, FloatProvider floorLevel)
        : this(new CarverConfiguration(probability, y, yScale, lavaLevel, CarverDebugSettings.DEFAULT, replaceable),
            horizontalRadiusMultiplier, verticalRadiusMultiplier, floorLevel)
    {
    }
}

//CanyonCarverConfiguration 峡谷雕刻配置对应原版 CanyonCarverConfiguration
public sealed class CanyonCarverConfiguration : CarverConfiguration
{
    public static readonly Codec<CanyonCarverConfiguration> Codec =
        RecordCodecBuilder.Of3<CanyonCarverConfiguration, CarverConfiguration, FloatProvider, CanyonShapeConfiguration>(
            new FieldCodec<CanyonCarverConfiguration, CarverConfiguration>(CarverConfiguration.Codec,
                c => (CarverConfiguration)c),
            FloatProviders.Codec.FieldOf("vertical_rotation")
                .ForGetter<CanyonCarverConfiguration, FloatProvider>(c => c.VerticalRotation),
            CanyonShapeConfiguration.Codec.FieldOf("shape")
                .ForGetter<CanyonCarverConfiguration, CanyonShapeConfiguration>(c => c.Shape),
            (baseConfig, verticalRotation, shape) => new CanyonCarverConfiguration(baseConfig, verticalRotation, shape));

    public FloatProvider VerticalRotation { get; }
    public CanyonShapeConfiguration Shape { get; }

    public CanyonCarverConfiguration(CarverConfiguration baseConfig, FloatProvider verticalRotation,
        CanyonShapeConfiguration shape)
        : base(baseConfig.Probability, baseConfig.Y, baseConfig.YScale, baseConfig.LavaLevel, baseConfig.DebugSettings,
            baseConfig.Replaceable)
    {
        VerticalRotation = verticalRotation;
        Shape = shape;
    }
}

//CanyonShapeConfiguration 峡谷形状配置对应原版 CanyonShapeConfiguration
public sealed class CanyonShapeConfiguration
{
    public static readonly Codec<CanyonShapeConfiguration> Codec =
        RecordCodecBuilder.Of6<CanyonShapeConfiguration, FloatProvider, FloatProvider, int, FloatProvider, float, float>(
            FloatProviders.Codec.FieldOf("distance_factor")
                .ForGetter<CanyonShapeConfiguration, FloatProvider>(s => s.DistanceFactor),
            FloatProviders.Codec.FieldOf("thickness").ForGetter<CanyonShapeConfiguration, FloatProvider>(s => s.Thickness),
            Codecs.Int.FieldOf("width_smoothness").ForGetter<CanyonShapeConfiguration, int>(s => s.WidthSmoothness),
            FloatProviders.Codec.FieldOf("horizontal_radius_factor")
                .ForGetter<CanyonShapeConfiguration, FloatProvider>(s => s.HorizontalRadiusFactor),
            Codecs.Float.FieldOf("vertical_radius_default_factor")
                .ForGetter<CanyonShapeConfiguration, float>(s => s.VerticalRadiusDefaultFactor),
            Codecs.Float.FieldOf("vertical_radius_center_factor")
                .ForGetter<CanyonShapeConfiguration, float>(s => s.VerticalRadiusCenterFactor),
            (distanceFactor, thickness, widthSmoothness, horizontalRadiusFactor, verticalRadiusDefaultFactor,
                verticalRadiusCenterFactor) => new CanyonShapeConfiguration(distanceFactor, thickness, widthSmoothness,
                horizontalRadiusFactor, verticalRadiusDefaultFactor, verticalRadiusCenterFactor));

    public FloatProvider DistanceFactor { get; }
    public FloatProvider Thickness { get; }
    public int WidthSmoothness { get; }
    public FloatProvider HorizontalRadiusFactor { get; }
    public float VerticalRadiusDefaultFactor { get; }
    public float VerticalRadiusCenterFactor { get; }

    public CanyonShapeConfiguration(FloatProvider distanceFactor, FloatProvider thickness, int widthSmoothness,
        FloatProvider horizontalRadiusFactor, float verticalRadiusDefaultFactor, float verticalRadiusCenterFactor)
    {
        DistanceFactor = distanceFactor;
        Thickness = thickness;
        WidthSmoothness = widthSmoothness;
        HorizontalRadiusFactor = horizontalRadiusFactor;
        VerticalRadiusDefaultFactor = verticalRadiusDefaultFactor;
        VerticalRadiusCenterFactor = verticalRadiusCenterFactor;
    }
}
