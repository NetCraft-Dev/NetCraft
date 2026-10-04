namespace NetCraft.Primitives;

//四分坐标对应原版net.minecraft.core.QuartPos
//把block坐标右移2位变quart坐标用于biome和noise相对位置
public static class QuartPos
{
    public const int Bits = 2;
    public const int Size = 4;
    public const int Mask = 3;

    //fromBlock把block坐标右移2位变quart坐标
    public static int FromBlock(int blockCoord) => blockCoord >> Bits;

    //toBlock把quart坐标左移2位变block坐标
    public static int ToBlock(int quartCoord) => quartCoord << Bits;
}
