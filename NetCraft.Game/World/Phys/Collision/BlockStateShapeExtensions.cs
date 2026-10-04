using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//BlockStateShapeExtensions 方块状态的形状入口 对应原版 BlockState 上的 getShape/getCollisionShape/getInteractionShape
//原版这几个方法挂 BlockState 上 BlockState 在 Registry 层 形状的默认实现要问 BlockBehaviour 在 Game 层
//按项目分层放到这里做扩展
public static class BlockStateShapeExtensions
{
    //GetShape 方块视觉形状 默认整块 对应原版 getShape
    public static VoxelShape GetShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetShape(state, level, pos, CollisionContext.Empty);

    public static VoxelShape GetShape(this BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => Behaviour(state).GetShape(state, level, pos, context);

    //GetCollisionShape 方块碰撞形状 对应原版 getCollisionShape
    public static VoxelShape GetCollisionShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetCollisionShape(state, level, pos, CollisionContext.Empty);

    public static VoxelShape GetCollisionShape(this BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => Behaviour(state).GetCollisionShape(state, level, pos, context);

    //GetInteractionShape 交互形状（准星射线命中盒） 对应原版 getInteractionShape
    public static VoxelShape GetInteractionShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetInteractionShape(state, level, pos);

    //GetBlockSupportShape 依附判定用形状 对应原版 getBlockSupportShape
    public static VoxelShape GetBlockSupportShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetBlockSupportShape(state, level, pos);

    //GetOcclusionShape 遮挡形状 对应原版 getOcclusionShape
    public static VoxelShape GetOcclusionShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetOcclusionShape(state, level, pos);

    //GetVisualShape 视觉形状 对应原版 getVisualShape
    public static VoxelShape GetVisualShape(this BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => Behaviour(state).GetVisualShape(state, level, pos, context);

    private static BlockBehaviour Behaviour(BlockState state)
        => state.Owner as BlockBehaviour
            ?? throw new InvalidOperationException($"方块 {state.Owner} 不是 BlockBehaviour");
}
