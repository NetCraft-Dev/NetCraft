using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//NoiseThresholdCountPlacement 噪声阈值计数放置对应原版 NoiseThresholdCountPlacement
//按噪声是否低于阈值在两组数量间二选一
public sealed class NoiseThresholdCountPlacement : RepeatingPlacement
{
    public static readonly Codec<NoiseThresholdCountPlacement> Codec =
        RecordCodecBuilder.Of3<NoiseThresholdCountPlacement, double, int, int>(
            Codecs.Double.FieldOf("noise_level")
                .ForGetter<NoiseThresholdCountPlacement, double>(placement => placement.NoiseLevel),
            Codecs.Int.FieldOf("below_noise")
                .ForGetter<NoiseThresholdCountPlacement, int>(placement => placement.BelowNoise),
            Codecs.Int.FieldOf("above_noise")
                .ForGetter<NoiseThresholdCountPlacement, int>(placement => placement.AboveNoise),
            (noiseLevel, belowNoise, aboveNoise) => new NoiseThresholdCountPlacement(noiseLevel, belowNoise, aboveNoise));

    public double NoiseLevel { get; }
    public int BelowNoise { get; }
    public int AboveNoise { get; }

    private NoiseThresholdCountPlacement(double noiseLevel, int belowNoise, int aboveNoise)
    {
        NoiseLevel = noiseLevel;
        BelowNoise = belowNoise;
        AboveNoise = aboveNoise;
    }

    //Of 构造入口对应原版 of
    public static NoiseThresholdCountPlacement Of(double noiseLevel, int belowNoise, int aboveNoise)
        => new(noiseLevel, belowNoise, aboveNoise);

    protected override int Count(RandomSource random, BlockPos origin)
    {
        var flowerNoise = BiomeInfoNoise.GetValue(origin.X / 200.0, origin.Z / 200.0);
        return flowerNoise < NoiseLevel ? BelowNoise : AboveNoise;
    }

    public override PlacementModifierType Type => NoiseThresholdCountPlacementType.Instance;
}

//NoiseThresholdCountPlacementType 对应原版 PlacementModifierType.NOISE_THRESHOLD_COUNT
public sealed class NoiseThresholdCountPlacementType : PlacementModifierType<NoiseThresholdCountPlacement>
{
    public static readonly NoiseThresholdCountPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("noise_threshold_count"), new NoiseThresholdCountPlacementType());

    private NoiseThresholdCountPlacementType()
        : base(Identifier.WithDefaultNamespace("noise_threshold_count"), NoiseThresholdCountPlacement.Codec) { }
}
