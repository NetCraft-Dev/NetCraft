using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//IBlockUpdateBehaviour, block update behavior contract, implemented by Game-layer block behavior classes
//Registry.Block carries data only; behavior is in the Game layer, the update chain calls back through this interface and does not depend back on Game
//Method signatures and semantics align with the same-named methods on vanilla BlockBehaviour
public interface IBlockUpdateBehaviour
{
    //NeighborChanged, callback after a neighboring block changes, maps to vanilla neighborChanged
    //changedBlock is the block that changed, not this block
    //The vanilla signature also has an orientation parameter; it is always null without experimental redstone, the parameter slot will be added when experimental redstone is wired in
    void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
        NetCraft.Registry.Block changedBlock, bool movedByPiston);

    //UpdateShape recomputes itself after a neighbor's shape changes, maps to vanilla updateShape, defaults to returning the current state
    //directionToNeighbour is the direction from this block toward the neighbor
    BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
        Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState);

    //UpdateIndirectNeighbourShapes, indirect shape update, maps to vanilla updateIndirectNeighbourShapes, no-op by default
    void UpdateIndirectNeighbourShapes(ServerLevel level, BlockPos pos, BlockState state,
        int updateFlags, int updateLimit);

    //OnPlace, callback after a block is placed, maps to vanilla onPlace
    void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState, bool movedByPiston);

    //AffectNeighborsAfterRemoval, extra effects on neighbors after this block is removed, maps to vanilla affectNeighborsAfterRemoval, no-op by default
    void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state, bool movedByPiston);

    //Tick, scheduled tick callback, maps to vanilla Block.tick, driven by the block tick
    void Tick(ServerLevel level, BlockPos pos, BlockState state, NetCraft.Util.Random.RandomSource random);

    //TriggerEvent, block event callback, returns whether it was handled, maps to vanilla Block.triggerEvent
    bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB);
}
