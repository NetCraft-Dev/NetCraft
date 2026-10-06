using NetCraft.Game.World.Level.Block;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//EmptyBlockGetter empty world view, maps to vanilla net.minecraft.world.level.EmptyBlockGetter
//Used when block shapes do not depend on the world, to save passing a real world layer; the vanilla height range is also all zero
public sealed class EmptyBlockGetter : BlockGetter
{
    public static readonly EmptyBlockGetter Instance = new();

    private EmptyBlockGetter() { }

    public int MinSectionY => 0;

    public int MaxSectionY => 0;

    public int SectionsCount => 0;

    //Out of bounds and unloaded are always air
    public BlockState GetBlockState(int x, int y, int z) => Blocks.AIR.DefaultBlockState;
}
