using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//PerlinNoise multi-octave Perlin noise, maps to vanilla net.minecraft.world.level.levelgen.synth.PerlinNoise
//Several ImprovedNoise layers at different frequencies sum into fractional Brownian motion (fBm)
//amplitudes weights each octave; firstOctave sets the starting octave
public class PerlinNoise
{
    private const double RoundOff = 3.3554432E7;
    //Reciprocal of RoundOff, 2^-25, is exactly representable, so dividing by it equals multiplying and matches bit for bit
    private const double InvRoundOff = 1.0 / 3.3554432E7;

    private readonly ImprovedNoise[] _noiseLevels;
    private readonly int _firstOctave;
    private readonly double[] _amplitudes;
    private readonly double _lowestFreqValueFactor;
    private readonly double _lowestFreqInputFactor;
    //Per-octave input scales and output weights are unrolled into arrays, so the sampling loop no longer does per-step multiply/divide
    //The recurrence is only multiplication and division by powers of two, so precomputing changes no floating-point bit
    private readonly double[] _inputScales;
    private readonly double[] _valueFactors;
    private readonly double _maxValue;

    //CreateLegacyForBlendedNoise legacy factory used by BlendedNoise, maps to vanilla createLegacyForBlendedNoise
    //Vanilla passes useNewFactory=false here for legacy initialization; this class names the flag forceLegacy, so pass true
    public static PerlinNoise CreateLegacyForBlendedNoise(RandomSource random, IEnumerable<int> octaves)
    {
        var (firstOctave, amplitudes) = MakeAmplitudes(new SortedSet<int>(octaves));
        return new PerlinNoise(random, firstOctave, amplitudes, true);
    }

    //CreateLegacyForLegacyNetherBiome legacy nether biome factory, maps to vanilla createLegacyForLegacyNetherBiome
    //Vanilla passes useNewFactory=false here for legacy initialization; this class names the flag forceLegacy, so pass true
    public static PerlinNoise CreateLegacyForLegacyNetherBiome(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes)
        => new(random, firstOctave, amplitudes, true);

    //Create new-style factory, maps to vanilla create(random, firstOctave, amplitudes)
    //Vanilla passes useNewFactory=true here, deriving through forkPositional.FromHashOf
    public static PerlinNoise Create(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes)
        => new(random, firstOctave, amplitudes, false);

    //MakeAmplitudes derives (firstOctave, amplitudes) from the octave set, aligned with vanilla makeAmplitudes
    //The octave set is usually a negative range; the derived amplitudes array holds 1.0 at those positions and 0 elsewhere
    private static (int FirstOctave, double[] Amplitudes) MakeAmplitudes(ISet<int> octaveSet)
    {
        if (octaveSet.Count == 0)
            throw new ArgumentException("Need some octaves!");
        var sorted = octaveSet.OrderBy(x => x).ToList();
        var lowFreqOctaves = -sorted[0];
        var highFreqOctaves = sorted[^1];
        var octaves = lowFreqOctaves + highFreqOctaves + 1;
        if (octaves < 1)
            throw new ArgumentException("Total number of octaves needs to be >= 1");
        var amplitudes = new double[octaves];
        foreach (var octave in sorted)
            amplitudes[octave + lowFreqOctaves] = 1.0;
        return (-lowFreqOctaves, amplitudes);
    }

    public PerlinNoise(RandomSource random, int firstOctave, params double[] amplitudes)
        : this(random, firstOctave, (IReadOnlyList<double>)amplitudes)
    {
    }

    public PerlinNoise(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes)
        : this(random, firstOctave, amplitudes, false)
    {
    }

    //forceLegacy forces the legacy Fork derivation path, maps to vanilla useNewInitialization=false
    //The legacy path matches vanilla createLegacyForBlendedNoise/createLegacyForLegacyNetherBiome
    //The new path derives a deterministic random source with forkPositional.FromHashOf
    public PerlinNoise(RandomSource random, int firstOctave, IReadOnlyList<double> amplitudes, bool forceLegacy)
    {
        _firstOctave = firstOctave;
        _amplitudes = amplitudes.ToArray();
        _noiseLevels = new ImprovedNoise[amplitudes.Count];
        var zeroOctaveIndex = -firstOctave;

        if (forceLegacy)
        {
            //The legacy path shares one random and builds octaves in order, aligning through consumption order like vanilla
            //Each ImprovedNoise advances the random by 262; positions with amplitude 0 only advance without building
            var zeroOctave = new ImprovedNoise(random);
            if (zeroOctaveIndex >= 0 && zeroOctaveIndex < amplitudes.Count && amplitudes[zeroOctaveIndex] != 0.0)
                _noiseLevels[zeroOctaveIndex] = zeroOctave;

            //Fill in the negative octaves from zeroOctaveIndex-1 downward; when amplitude is 0, skip with one random advance
            for (var i = zeroOctaveIndex - 1; i >= 0; i--)
            {
                if (i < amplitudes.Count)
                {
                    if (amplitudes[i] != 0.0)
                        _noiseLevels[i] = new ImprovedNoise(random);
                    else
                        random.ConsumeCount(262);
                }
                else
                {
                    random.ConsumeCount(262);
                }
            }
        }
        else
        {
            //The new path derives a deterministic random source with forkPositional.FromHashOf, maps to vanilla fromHashOf("octave_"+(firstOctave+i))
            var positional = random.ForkPositional();
            for (var i = 0; i < amplitudes.Count; i++)
            {
                if (amplitudes[i] != 0.0)
                    _noiseLevels[i] = new ImprovedNoise(positional.FromHashOf("octave_" + (firstOctave + i)));
            }
        }

        _lowestFreqInputFactor = Math.Pow(2.0, -zeroOctaveIndex);
        _lowestFreqValueFactor = Math.Pow(2.0, amplitudes.Count - 1) / (Math.Pow(2.0, amplitudes.Count) - 1.0);
        _maxValue = EdgeValue(2.0);
    }

    //GetValue 3-argument sampling, maps to vanilla getValue(x,y,z)
    //Takes a fast path: calls the 3-argument ImprovedNoise noise directly, skipping the yScale/yFudge branch and extra smoothing
    //With yScale/yFudge at 0 the two are mathematically equivalent and match bit for bit
    public double GetValue(double x, double y, double z)
    {
        var value = 0.0;
        var factor = _lowestFreqInputFactor;
        var valueFactor = _lowestFreqValueFactor;
        //Cache both tables once; the sampling loop no longer goes through instance fields each step
        var levels = _noiseLevels;
        var amplitudes = _amplitudes;
        for (var i = 0; i < levels.Length; i++)
        {
            var noise = levels[i];
            if (noise is not null)
                value += amplitudes[i] * noise.Noise(Wrap(x * factor), Wrap(y * factor), Wrap(z * factor))
                    * valueFactor;
            factor *= 2.0;
            valueFactor /= 2.0;
        }
        return value;
    }

    //GetValue 5-argument sampling, maps to vanilla getValue(x,y,z,yScale,yFudge)
    //BlendedNoise passes yScale/yFudge to get the y-direction step offset for low octaves
    public double GetValue(double x, double y, double z, double yScale, double yFudge)
    {
        var value = 0.0;
        var factor = _lowestFreqInputFactor;
        var valueFactor = _lowestFreqValueFactor;
        var levels = _noiseLevels;
        var amplitudes = _amplitudes;
        for (var i = 0; i < levels.Length; i++)
        {
            var noise = levels[i];
            if (noise is not null)
            {
                var noiseVal = noise.Noise(Wrap(x * factor), Wrap(y * factor), Wrap(z * factor),
                    yScale * factor, yFudge * factor);
                value += amplitudes[i] * noiseVal * valueFactor;
            }
            factor *= 2.0;
            valueFactor /= 2.0;
        }
        return value;
    }

    public double MaxValue => _maxValue;

    //MaxBrokenValue max value for a given yScale, maps to vanilla maxBrokenValue
    //BlendedNoise uses yMultiplier as yScale to compute the actual max value
    public double MaxBrokenValue(double yScale)
        => EdgeValue(yScale + 2.0);

    //GetOctaveNoise fetch the i-th octave (indexed high to low), maps to vanilla getOctaveNoise
    public ImprovedNoise GetOctaveNoise(int i)
        => _noiseLevels[_noiseLevels.Length - 1 - i];

    //EdgeValue compute the boundary value for a given noiseValue using the accumulated valueFactor, maps to vanilla edgeValue
    //Vanilla walks all non-null noiseLevels and accumulates amplitudes[i]*noiseValue*valueFactor
    private double EdgeValue(double noiseValue)
    {
        var value = 0.0;
        var valueFactor = _lowestFreqValueFactor;
        for (var i = 0; i < _noiseLevels.Length; i++)
        {
            if (_noiseLevels[i] is not null)
                value += _amplitudes[i] * noiseValue * valueFactor;
            valueFactor /= 2.0;
        }
        return value;
    }

    //wrap prevents precision loss at large coordinates, aligned with vanilla wrap
    public static double Wrap(double x)
        => x - Math.Floor(x * InvRoundOff + 0.5) * RoundOff;
}
