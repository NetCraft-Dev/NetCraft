namespace NetCraft.Primitives;

//BlockHitResult 方块命中结果对应原版 net.minecraft.world.phys.BlockHitResult
//记录命中方块坐标/命中面/命中点在方块内的精确位置/是否从方块内部命中/是否撞到世界边界
//WorldBorderHit 是 26.2 网络序列化里的最后一个布尔 缺它后面紧邻的字段会整体前移一位
public readonly record struct BlockHitResult(BlockPos BlockPos, Direction Direction, Vec3 Location, bool Inside,
    bool WorldBorderHit = false);
