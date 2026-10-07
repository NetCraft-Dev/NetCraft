using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//NoiseBasedCountPlacement noise-based count placement, maps to vanilla NoiseBasedCountPlacement
//The count is derived from the biome info noise by coordinate
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

    //Of construction entry, maps to vanilla of
    public static NoiseBasedCountPlacement Of(int noiseToCountRatio, double noiseFactor, double noiseOffset)
        => new(noiseToCountRatio, noiseFactor, noiseOffset);

    protected override int Count(RandomSource random, BlockPos origin)
    {
        var flowerNoise = BiomeInfoNoise.GetValue(origin.X / NoiseFactor, origin.Z / NoiseFactor);
        return (int)Math.Ceiling((flowerNoise + NoiseOffset) * NoiseToCountRatio);
    }

    public override PlacementModifierType Type => NoiseBasedCountPlacementType.Instance;
}

//NoiseBasedCountPlacementType, maps to vanilla PlacementModifierType.NOISE_BASED_COUNT
public sealed class NoiseBasedCountPlacementType : PlacementModifierType<NoiseBasedCountPlacement>
{
    public static readonly NoiseBasedCountPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("noise_based_count"), new NoiseBasedCountPlacementType());

    private NoiseBasedCountPlacementType()
        : base(Identifier.WithDefaultNamespace("noise_based_count"), NoiseBasedCountPlacement.Codec) { }
}
