namespace NetCraft.Primitives;

//Quart position, maps to vanilla net.minecraft.core.QuartPos
//Shifts block coordinates right by 2 bits into quart coordinates, used for biome and noise relative positions
public static class QuartPos
{
    public const int Bits = 2;
    public const int Size = 4;
    public const int Mask = 3;

    //fromBlock shifts block coordinates right by 2 bits into quart coordinates
    public static int FromBlock(int blockCoord) => blockCoord >> Bits;

    //toBlock shifts quart coordinates left by 2 bits into block coordinates
    public static int ToBlock(int quartCoord) => quartCoord << Bits;
}
