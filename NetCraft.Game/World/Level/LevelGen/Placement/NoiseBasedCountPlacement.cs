using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//NoiseBasedCountPlacement 噪声计数放置对应原版 NoiseBasedCountPlacement
//数量由群系信息噪声按坐标换算
public sealed class NoiseBasedCountPlacement : RepeatingPlacement
{
    public static readonly Codec<NoiseBasedCountPlacement> Codec =
        RecordCodecBuilder.Of3<NoiseBasedCountPlacement, int, double, double>(
            Codecs.Int.FieldOf("noise_to_count_ratio")
                .ForGetter<NoiseBasedCountPlacement, int>(placement => placement.NoiseToCountRatio),
            Codecs.Double.FieldOf("noise_factor")
                .ForGetter<NoiseBasedCountPlacement, double>(placement => placement.NoiseFactor),
            Codecs.Double.OptionalFieldOf("noise_offset", 0.0)
                .ForGetter<NoiseBasedCountPlacement, double>(placement => placement.NoiseOffset),
            (ratio, factor, offset) => new NoiseBasedCountPlacement(ratio, factor, offset));

    public int NoiseToCountRatio { get; }
    public double NoiseFactor { get; }
    public double NoiseOffset { get; }

    private NoiseBasedCountPlacement(int noiseToCountRatio, double noiseFactor, double noiseOffset)
    {
        NoiseToCountRatio = noiseToCountRatio;
        NoiseFactor = noiseFactor;
        NoiseOffset = noiseOffset;
    }

    //Of 构造入口对应原版 of
    public static NoiseBasedCountPlacement Of(int noiseToCountRatio, double noiseFactor, double noiseOffset)
        => new(noiseToCountRatio, noiseFactor, noiseOffset);

    protected override int Count(RandomSource random, BlockPos origin)
    {
        var flowerNoise = BiomeInfoNoise.GetValue(origin.X / NoiseFactor, origin.Z / NoiseFactor);
        return (int)Math.Ceiling((flowerNoise + NoiseOffset) * NoiseToCountRatio);
    }

    public override PlacementModifierType Type => NoiseBasedCountPlacementType.Instance;
}

//NoiseBasedCountPlacementType 对应原版 PlacementModifierType.NOISE_BASED_COUNT
public sealed class NoiseBasedCountPlacementType : PlacementModifierType<NoiseBasedCountPlacement>
{
    public static readonly NoiseBasedCountPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("noise_based_count"), new NoiseBasedCountPlacementType());

    private NoiseBasedCountPlacementType()
        : base(Identifier.WithDefaultNamespace("noise_based_count"), NoiseBasedCountPlacement.Codec) { }
}
