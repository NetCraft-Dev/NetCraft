using NetCraft.Primitives;

namespace NetCraft.Storage;

//BlockEventData 方块事件 对应原版 net.minecraft.world.level.BlockEventData
//四个字段全参与判重 同样的位置方块与参数只会入队一次
//活塞 箱子开合 音符盒一类都走这条通道 与调度刻是两条独立的路
public readonly record struct BlockEventData(BlockPos Pos, NetCraft.Registry.Block Block, int ParamA, int ParamB);
