using NetCraft.Game.World.Level.Block;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//EmptyBlockGetter 空世界视图 对应原版 net.minecraft.world.level.EmptyBlockGetter
//方块形状不依赖世界时用它可以少传一层真实世界 原版高度范围也是全零
public sealed class EmptyBlockGetter : BlockGetter
{
    public static readonly EmptyBlockGetter Instance = new();

    private EmptyBlockGetter() { }

    public int MinSectionY => 0;

    public int MaxSectionY => 0;

    public int SectionsCount => 0;

    //越界与未加载一律空气
    public BlockState GetBlockState(int x, int y, int z) => Blocks.AIR.DefaultBlockState;
}
