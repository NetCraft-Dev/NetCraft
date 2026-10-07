using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//BlendedNoise blended noise, maps to vanilla net.minecraft.world.level.levelgen.synth.BlendedNoise
//Implements SimpleFunction as the main terrain noise source
//8-octave mainNoise drives smooth blending; 16-octave minLimit/maxLimit supply high and low frequency detail
//compute interpolates between minLimit/maxLimit using mainNoise to get the blended density
public sealed class BlendedNoise : SimpleFunction
{
    private readonly PerlinNoise _minLimitNoise;
    private readonly PerlinNoise _maxLimitNoise;
    private readonly PerlinNoise _mainNoise;
    private readonly double _xzMultiplier;
    private readonly double _yMultiplier;
    private readonly double _xzFactor;
    private readonly double _yFactor;
    private readonly double _smearScaleMultiplier;
    private readonly double _maxValue;
    private readonly double _xzScale;
    private readonly double _yScale;

    //CreateUnseeded unseeded factory, maps to vanilla createUnseeded
    //Acts as a placeholder during deserialization; withNewRandom injects the real random source afterwards
    public static BlendedNoise CreateUnseeded(double xzScale, double yScale, double xzFactor, double yFactor, double smearScaleMultiplier)
        => new(new XoroshiroRandomSource(0L), xzScale, yScale, xzFactor, yFactor, smearScaleMultiplier);

    //Constructor, maps to the vanilla @VisibleForTesting constructor
    //The three PerlinNoise instances use the CreateLegacyForBlendedNoise path with -15..0 and -7..0 octaves
    public BlendedNoise(RandomSource random, double xzScale, double yScale, double xzFactor, double yFactor, double smearScaleMultiplier)
    {
        _minLimitNoise = PerlinNoise.CreateLegacyForBlendedNoise(random, RangeClosed(-15, 0));
        _maxLimitNoise = PerlinNoise.CreateLegacyForBlendedNoise(random, RangeClosed(-15, 0));
        _mainNoise = PerlinNoise.CreateLegacyForBlendedNoise(random, RangeClosed(-7, 0));
        _xzScale = xzScale;
        _yScale = yScale;
        _xzFactor = xzFactor;
        _yFactor = yFactor;
        _smearScaleMultiplier = smearScaleMultiplier;
        _xzMultiplier = 684.412 * xzScale;
        _yMultiplier = 684.412 * yScale;
        _maxValue = _minLimitNoise.MaxBrokenValue(_yMultiplier);
    }

    //WithNewRandom inject a new random source and return a new instance, maps to vanilla withNewRandom
    //RandomState calls this during construction to replace the placeholder BlendedNoise with a seeded instance
    public BlendedNoise WithNewRandom(RandomSource terrainRandom)
        => new(terrainRandom, _xzScale, _yScale, _xzFactor, _yFactor, _smearScaleMultiplier);

    public double Compute(FunctionContext context)
        => ComputeAt(context.BlockX, context.BlockY, context.BlockZ);

    //ComputeAt evaluate at three block coordinates; the batch path loops over points and calls this
    internal double ComputeAt(int blockX, int blockY, int blockZ)
    {
        double blendMin = 0.0;
        double blendMax = 0.0;
        double mainNoiseValue = 0.0;
        var limitX = blockX * _xzMultiplier;
        var limitY = blockY * _yMultiplier;
        var limitZ = blockZ * _xzMultiplier;
        var mainX = limitX / _xzFactor;
        var mainY = limitY / _yFactor;
        var mainZ = limitZ / _xzFactor;
        var limitSmear = _yMultiplier * _smearScaleMultiplier;
        var mainSmear = limitSmear / _yFactor;

        var pow = 1.0;
        for (var i = 0; i < 8; i++)
        {
            var noise = _mainNoise.GetOctaveNoise(i);
            if (noise is not null)
                mainNoiseValue += noise.Noise(PerlinNoise.Wrap(mainX * pow), PerlinNoise.Wrap(mainY * pow), PerlinNoise.Wrap(mainZ * pow), mainSmear * pow, mainY * pow) / pow;
            pow /= 2.0;
        }
        var factor = (mainNoiseValue / 10.0 + 1.0) / 2.0;
        var isMax = factor >= 1.0;
        var isMin = factor <= 0.0;

        var pow2 = 1.0;
        for (var i = 0; i < 16; i++)
        {
            var wx = PerlinNoise.Wrap(limitX * pow2);
            var wy = PerlinNoise.Wrap(limitY * pow2);
            var wz = PerlinNoise.Wrap(limitZ * pow2);
            var yScalePow = limitSmear * pow2;
            if (!isMax)
            {
                var minNoise = _minLimitNoise.GetOctaveNoise(i);
                if (minNoise is not null)
                    blendMin += minNoise.Noise(wx, wy, wz, yScalePow, limitY * pow2) / pow2;
            }
            if (!isMin)
            {
                var maxNoise = _maxLimitNoise.GetOctaveNoise(i);
                if (maxNoise is not null)
                    blendMax += maxNoise.Noise(wx, wy, wz, yScalePow, limitY * pow2) / pow2;
            }
            pow2 /= 2.0;
        }
        return Mth.ClampedLerp(factor, blendMin / 512.0, blendMax / 512.0) / 128.0;
    }

    public double MinValue => -MaxValue;

    public double MaxValue => _maxValue;

    //The five scale parameters exposed for codec serialization
    public double XzScale => _xzScale;
    public double YScale => _yScale;
    public double XzFactor => _xzFactor;
    public double YFactor => _yFactor;
    public double SmearScaleMultiplier => _smearScaleMultiplier;

    //RangeClosed mimics Java IntStream.rangeClosed(from, to), inclusive
    private static IEnumerable<int> RangeClosed(int from, int to)
    {
        for (var i = from; i <= to; i++)
            yield return i;
    }
}
