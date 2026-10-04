namespace NetCraft.Util.Random;

//Marsaglia极坐标高斯分布对应原版net.minecraft.world.level.levelgen.MarsagliaPolarGaussian
//包装RandomSource生成标准正态分布值缓存下一值避免重算
public sealed class MarsagliaPolarGaussian
{
    //randomSource底层随机源对应原版randomSource字段public供调试访问
    public RandomSource RandomSource { get; }
    private double _nextNextGaussian;
    private bool _haveNextNextGaussian;

    public MarsagliaPolarGaussian(RandomSource randomSource)
    {
        RandomSource = randomSource;
    }

    //reset清除缓存对应原版reset
    public void Reset() => _haveNextNextGaussian = false;

    //nextGaussian生成标准正态分布值对应原版nextGaussian
    //Marsaglia极坐标法拒绝采样后两个独立高斯值一个返回一个缓存
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
