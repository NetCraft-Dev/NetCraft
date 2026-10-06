using System.Text;
using NetCraft.Primitives;

namespace NetCraft.Util.Random;

//Positional random factory interface, maps to vanilla net.minecraft.world.level.levelgen.PositionalRandomFactory
//Generates a stable RandomSource from position or string, used by worldgen to stay deterministic
public interface PositionalRandomFactory
{
    //fromHashOf generates a random source from a string hash
    RandomSource FromHashOf(string name);

    //fromSeed generates a random source from a seed
    RandomSource FromSeed(long seed);

    //at generates a random source from coordinates
    RandomSource At(int x, int y, int z);

    //parityConfigString outputs parity check info for debugging, maps to vanilla parityConfigString
    void ParityConfigString(StringBuilder sb);

    //at BlockPos overload, maps to vanilla at(BlockPos)
    RandomSource At(Vec3i pos) => At(pos.X, pos.Y, pos.Z);
}
