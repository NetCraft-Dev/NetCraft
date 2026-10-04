using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//SupportType 依附面判定强度 对应原版 net.minecraft.world.level.block.SupportType
//FULL 整面都得顶住 CENTER 只要中心柱体顶住 RIGID 要一圈边框都顶住
//命名空间段名 Block 与 Registry.Block 类型同名 这里引用方块类一律写完全限定名
public enum SupportType
{
    Full,
    Center,
    Rigid,
}

//SupportTypeExtensions 三种强度的判定 对应原版枚举里各常量的 isSupporting
public static class SupportTypeExtensions
{
    //CENTER_SUPPORT_SHAPE 中心 2/16 见方 高到 10/16 的柱体
    private static readonly VoxelShape CenterSupportShape = NetCraft.Registry.Block.Column(2.0, 0.0, 10.0);

    //RIGID_SUPPORT_SHAPE 整块挖掉中心 12/16 柱体 剩下的一圈边框
    private static readonly VoxelShape RigidSupportShape = Shapes.Join(
        Shapes.Block(), NetCraft.Registry.Block.Column(12.0, 0.0, 16.0), BooleanOps.OnlyFirst);

    //IsSupporting 该强度下这一面算不算顶住 对应原版 isSupporting
    public static bool IsSupporting(this SupportType supportType, BlockState state, BlockGetter level, BlockPos pos,
        Direction direction)
    {
        var face = state.GetBlockSupportShape(level, pos).GetFaceShape(direction);
        return supportType switch
        {
            SupportType.Full => NetCraft.Registry.Block.IsShapeFullBlock(face),
            SupportType.Center => !Shapes.JoinIsNotEmpty(face, CenterSupportShape, BooleanOps.OnlySecond),
            _ => !Shapes.JoinIsNotEmpty(face, RigidSupportShape, BooleanOps.OnlySecond),
        };
    }
}
