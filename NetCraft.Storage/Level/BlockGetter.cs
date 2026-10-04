using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage;

//BlockGetter 方块读取接口对应原版 net.minecraft.world.level.BlockGetter
//原版继承 LevelHeightAccessor 光照引擎取世界高度范围也走它
public interface BlockGetter : LevelHeightAccessor
{
    //getBlockState 按世界坐标取方块状态 越界返回空气
    BlockState GetBlockState(int x, int y, int z);
}
