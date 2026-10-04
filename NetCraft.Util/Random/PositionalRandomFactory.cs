using System.Text;
using NetCraft.Primitives;

namespace NetCraft.Util.Random;

//位置性随机工厂接口对应原版net.minecraft.world.level.levelgen.PositionalRandomFactory
//按位置或字符串生成稳定RandomSource用于世界生成保持确定性
public interface PositionalRandomFactory
{
    //fromHashOf按字符串哈希生成随机源
    RandomSource FromHashOf(string name);

    //fromSeed按种子生成随机源
    RandomSource FromSeed(long seed);

    //at按坐标生成随机源
    RandomSource At(int x, int y, int z);

    //parityConfigString输出奇偶校验信息用于调试对应原版parityConfigString
    void ParityConfigString(StringBuilder sb);

    //at按BlockPos重载对应原版at(BlockPos)
    RandomSource At(Vec3i pos) => At(pos.X, pos.Y, pos.Z);
}
