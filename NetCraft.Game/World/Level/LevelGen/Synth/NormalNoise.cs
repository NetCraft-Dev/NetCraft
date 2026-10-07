using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//NormalNoise normal-distribution noise, maps to vanilla net.minecraft.world.level.levelgen.synth.NormalNoise
//Two PerlinNoise instances combine into normal-distribution noise
//INPUT_FACTOR decorrelates the two noises; valueFactor scales to the target standard deviation
public sealed class NormalNoise
{
    private const double InputFactor = 1.0181268882175227;

    private readonly double _valueFactor;
    private readonly PerlinNoise _first;
    private readonly PerlinNoise _second;
    private readonly double _maxValue;
    private readonly NoiseParameters _parameters;

    //Create new-style factory, maps to vanilla create; derives through forkPositional
    public static NormalNoise Create(RandomSource random, int firstOctave, params double[] amplitudes)
        => Create(random, new NoiseParameters(firstOctave, amplitudes));

    //Create new-style factory taking NoiseParameters, maps to vanilla create(random, parameters)
    public static NormalNoise Create(RandomSource random, NoiseParameters parameters)
        => new(random, parameters, true);

    //CreateLegacyNetherBiome legacy nether biome factory, maps to vanilla createLegacyNetherBiome
    //Uses the PerlinNoise.CreateLegacyForLegacyNetherBiome path to keep the legacy determinism
    public static NormalNoise CreateLegacyNetherBiome(RandomSource random, NoiseParameters parameters)
        => new(random, parameters, false);

    //Private constructor taking useNewInitialization, maps to the vanilla private constructor
    //true derives PerlinNoise through the new forkPositional path, false through the legacy Fork path
    private NormalNoise(RandomSource random, NoiseParameters parameters, bool useNewInitialization)
    {
        _parameters = parameters;
        var firstOctave = parameters.FirstOctave;
        var amplitudes = parameters.Amplitudes;
        if (useNewInitialization)
        {
            _first = PerlinNoise.Create(random, firstOctave, amplitudes);
            _second = PerlinNoise.Create(random, firstOctave, amplitudes);
        }
        else
        {
            _first = PerlinNoise.CreateLegacyForLegacyNetherBiome(random, firstOctave, amplitudes);
            _second = PerlinNoise.CreateLegacyForLegacyNetherBiome(random, firstOctave, amplitudes);
        }

        var minOctave = int.MaxValue;
        var maxOctave = int.MinValue;
        for (var i = 0; i < amplitudes.Count; i++)
        {
            if (amplitudes[i] != 0.0)
            {
                if (i < minOctave) minOctave = i;
                if (i > maxOctave) maxOctave = i;
            }
        }
        var expectedDev = ExpectedDeviation(maxOctave - minOctave);
        _valueFactor = 0.16666666666666666 / expectedDev;
        _maxValue = (_first.MaxValue + _second.MaxValue) * _valueFactor;
    }

    //Kept for old tests and simple callers; uses the new path
    public NormalNoise(RandomSource random, int firstOctave, params double[] amplitudes)
        : this(random, new NoiseParameters(firstOctave, amplitudes), true) { }

    public double GetValue(double x, double y, double z)
        => (_first.GetValue(x, y, z) + _second.GetValue(x * InputFactor, y * InputFactor, z * InputFactor)) * _valueFactor;

    public double MaxValue => _maxValue;

    public NoiseParameters Parameters => _parameters;

    private static double ExpectedDeviation(int octaveSpan)
        => 0.1 * (1.0 + 1.0 / (octaveSpan + 1));
}
