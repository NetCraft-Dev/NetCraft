using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage;

//BlockGetter, block read interface, maps to vanilla net.minecraft.world.level.BlockGetter
//Vanilla extends LevelHeightAccessor; the light engine also gets the world height range through it
public interface BlockGetter : LevelHeightAccessor
{
    //getBlockState returns the block state at world coords; out of range returns air
    BlockState GetBlockState(int x, int y, int z);
}
