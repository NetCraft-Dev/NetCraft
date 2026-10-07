using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//PerlinSimplexNoise multi-octave 2D simplex noise, maps to vanilla net.minecraft.world.level.levelgen.synth.PerlinSimplexNoise
//octaveSet is the octave set and its elements must be non-positive; commonly used for biome temperature and surface perturbation like icebergs and badlands
public sealed class PerlinSimplexNoise
{
    //ConsumeRounds number of random values consumed when skipping an octave
    //Vanilla uses InputConstants.KEY_RIGHT (right arrow keycode 262); it must be copied exactly to align with the random sequence
    private const int ConsumeRounds = 262;

    private readonly SimplexNoise?[] _noiseLevels;
    private readonly double _highestFreqValueFactor;
    private readonly double _highestFreqInputFactor;

    public PerlinSimplexNoise(RandomSource random, IReadOnlyList<int> octaveSet)
    {
        //Vanilla uses an IntRBTreeSet to sort and deduplicate
        var octaves = new SortedSet<int>(octaveSet);
        if (octaves.Count == 0)
            throw new ArgumentException("Need some octaves!");
        var lowFreqOctaves = -octaves.Min;
        var highFreqOctaves = octaves.Max;
        var count = lowFreqOctaves + highFreqOctaves + 1;
        if (count < 1)
            throw new ArgumentException("Total number of octaves needs to be >= 1");

        var zeroOctave = new SimplexNoise(random);
        _noiseLevels = new SimplexNoise?[count];
        if (highFreqOctaves >= 0 && highFreqOctaves < count && octaves.Contains(0))
            _noiseLevels[highFreqOctaves] = zeroOctave;
        for (var i = highFreqOctaves + 1; i < count; i++)
        {
            if (i >= 0 && octaves.Contains(highFreqOctaves - i))
                _noiseLevels[i] = new SimplexNoise(random);
            else
                random.ConsumeCount(ConsumeRounds);
        }
        if (highFreqOctaves > 0)
        {
            //High-frequency octaves derive their seed from one sample of the zero octave, maps to vanilla positiveOctaveSeed
            var positiveOctaveSeed = (long)(zeroOctave.GetValue(zeroOctave.Xo, zeroOctave.Yo, zeroOctave.Zo)
                * 9.223372036854776E18);
            var highFreqRandom = new LegacyRandomSource(positiveOctaveSeed);
            for (var i = highFreqOctaves - 1; i >= 0; i--)
            {
                if (i < count && octaves.Contains(highFreqOctaves - i))
                    _noiseLevels[i] = new SimplexNoise(highFreqRandom);
                else
                    highFreqRandom.ConsumeCount(ConsumeRounds);
            }
        }
        _highestFreqInputFactor = Math.Pow(2.0, highFreqOctaves);
        _highestFreqValueFactor = 1.0 / (Math.Pow(2.0, count) - 1.0);
    }

    //GetValue 2D sampling, maps to vanilla getValue
    //With useNoiseStart true each octave adds its own origin offset; biome temperature passes false
    public double GetValue(double x, double y, bool useNoiseStart)
    {
        var value = 0.0;
        var factor = _highestFreqInputFactor;
        var valueFactor = _highestFreqValueFactor;
        foreach (var level in _noiseLevels)
        {
            if (level is not null)
                value += level.GetValue(
                    x * factor + (useNoiseStart ? level.Xo : 0.0),
                    y * factor + (useNoiseStart ? level.Yo : 0.0)) * valueFactor;
            factor /= 2.0;
            valueFactor *= 2.0;
        }
        return value;
    }
}
