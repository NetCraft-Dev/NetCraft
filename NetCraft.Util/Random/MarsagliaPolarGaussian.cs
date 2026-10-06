namespace NetCraft.Util.Random;

//Marsaglia polar Gaussian distribution, maps to vanilla net.minecraft.world.level.levelgen.MarsagliaPolarGaussian
//Wraps RandomSource to generate standard normal values, caching the next value to avoid recomputation
public sealed class MarsagliaPolarGaussian
{
    //randomSource underlying random source, maps to vanilla randomSource field, public for debug access
    public RandomSource RandomSource { get; }
    private double _nextNextGaussian;
    private bool _haveNextNextGaussian;

    public MarsagliaPolarGaussian(RandomSource randomSource)
    {
        RandomSource = randomSource;
    }

    //reset clears the cache, maps to vanilla reset
    public void Reset() => _haveNextNextGaussian = false;

    //nextGaussian generates a standard normal value, maps to vanilla nextGaussian
    //Marsaglia polar method: after rejection sampling, two independent Gaussian values, one returned and one cached
    public double NextGaussian()
    {
        if (_haveNextNextGaussian)
        {
            _haveNextNextGaussian = false;
            return _nextNextGaussian;
        }
        while (true)
        {
            var x = 2.0 * RandomSource.NextDouble() - 1.0;
            var y = 2.0 * RandomSource.NextDouble() - 1.0;
            var radiusSquared = Mth.Square(x) + Mth.Square(y);
            if (radiusSquared < 1.0 && radiusSquared != 0.0)
            {
                var multiplier = Math.Sqrt(-2.0 * Math.Log(radiusSquared) / radiusSquared);
                _nextNextGaussian = y * multiplier;
                _haveNextNextGaussian = true;
                return x * multiplier;
            }
        }
    }
}
