using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block.Dispenser;

//BlockSource 发射器的发射上下文 对应原版 net.minecraft.core.dispenser.BlockSource
//发射行为需要的四样东西都在这里 关卡 位置 状态 方块实体
public readonly record struct BlockSource(
    ServerLevel Level,
    BlockPos Pos,
    BlockState State,
    DispenserBlockEntity BlockEntity)
{
    //Center 方块中心点 对应原版 Vec3.atCenterOf
    public Vec3 Center => new(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
}
