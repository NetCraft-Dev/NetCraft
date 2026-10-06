using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//BlockStateShapeExtensions shape entry points on a block state, maps to vanilla BlockState's getShape/getCollisionShape/getInteractionShape
//Vanilla hangs these methods on BlockState, which lives in the Registry layer, while the default shape implementation must ask BlockBehaviour in the Game layer
//Split into this extension per the project layering
public static class BlockStateShapeExtensions
{
    //GetShape block visual shape, defaults to a full block, maps to vanilla getShape
    public static VoxelShape GetShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetShape(state, level, pos, CollisionContext.Empty);

    public static VoxelShape GetShape(this BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => Behaviour(state).GetShape(state, level, pos, context);

    //GetCollisionShape block collision shape, maps to vanilla getCollisionShape
    public static VoxelShape GetCollisionShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetCollisionShape(state, level, pos, CollisionContext.Empty);

    public static VoxelShape GetCollisionShape(this BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => Behaviour(state).GetCollisionShape(state, level, pos, context);

    //GetInteractionShape interaction shape (crosshair ray hit box), maps to vanilla getInteractionShape
    public static VoxelShape GetInteractionShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetInteractionShape(state, level, pos);

    //GetBlockSupportShape shape used for support checks, maps to vanilla getBlockSupportShape
    public static VoxelShape GetBlockSupportShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetBlockSupportShape(state, level, pos);

    //GetOcclusionShape occlusion shape, maps to vanilla getOcclusionShape
    public static VoxelShape GetOcclusionShape(this BlockState state, BlockGetter level, BlockPos pos)
        => Behaviour(state).GetOcclusionShape(state, level, pos);

    //GetVisualShape visual shape, maps to vanilla getVisualShape
    public static VoxelShape GetVisualShape(this BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => Behaviour(state).GetVisualShape(state, level, pos, context);

    private static BlockBehaviour Behaviour(BlockState state)
        => state.Owner as BlockBehaviour
            ?? throw new InvalidOperationException($"block {state.Owner} is not a BlockBehaviour");
}
