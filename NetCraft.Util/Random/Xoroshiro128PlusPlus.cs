using System.Numerics;

namespace NetCraft.Util.Random;

//Xoroshiro128++核心随机数生成器对应原版net.minecraft.world.level.levelgen.Xoroshiro128PlusPlus
//双64位状态seedLo/seedHi位运算完全保留保证跨语言序列一致
public sealed class Xoroshiro128PlusPlus
{
    private long _seedLo;
    private long _seedHi;

    //按Seed128bit构造对应原版Xoroshiro128PlusPlus(Seed128bit)
    public Xoroshiro128PlusPlus(RandomSupport.Seed128bit seed)
        : this(seed.SeedLo, seed.SeedHi) { }

    //按双long构造对应原版Xoroshiro128PlusPlus(long,long)
    //全零状态会破坏生成器替换为黄金白银比例避免退化
    public Xoroshiro128PlusPlus(long seedLo, long seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
        if ((_seedLo | _seedHi) == 0)
        {
            _seedLo = RandomSupport.GoldenRatio64;
            _seedHi = RandomSupport.SilverRatio64;
        }
    }

    //nextLong生成下一个64位值对应原版nextLong
    //位运算含rotateLeft异或左移C#用unchecked保证long溢出wrap与Java一致
    //BitOperations.RotateLeft显式ulong强转对齐Java Long.rotateLeft无符号循环左移
    public long NextLong()
    {
        unchecked
        {
            var s0 = _seedLo;
            var s1 = _seedHi;
            var result = (long)BitOperations.RotateLeft((ulong)(s0 + s1), 17) + s0;
            var s12 = s1 ^ s0;
            _seedLo = (long)BitOperations.RotateLeft((ulong)s0, 49) ^ s12 ^ (s12 << 21);
            _seedHi = (long)BitOperations.RotateLeft((ulong)s12, 28);
            return result;
        }
    }
}
