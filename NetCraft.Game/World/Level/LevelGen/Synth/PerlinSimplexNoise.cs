using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//PerlinSimplexNoise 多八度二维单纯形噪声对应原版 net.minecraft.world.level.levelgen.synth.PerlinSimplexNoise
//octaveSet 是八度集合 元素必须非正 常用于群系温度与冰山/恶地一类的地表扰动
public sealed class PerlinSimplexNoise
{
    //ConsumeRounds 跳过八度时消耗的随机数个数
    //原版写的是 InputConstants.KEY_RIGHT(右方向键码 262) 必须照抄才能与随机数序列对齐
    private const int ConsumeRounds = 262;

    private readonly SimplexNoise?[] _noiseLevels;
    private readonly double _highestFreqValueFactor;
    private readonly double _highestFreqInputFactor;

    public PerlinSimplexNoise(RandomSource random, IReadOnlyList<int> octaveSet)
    {
        //原版用 IntRBTreeSet 排序去重
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
            //高频八度由零八度的一个采样值派生根种子 对应原版 positiveOctaveSeed
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

    //GetValue 二维采样对应原版 getValue
    //useNoiseStart 为 true 时叠加各八度自身的原点偏移 群系温度走 false
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
