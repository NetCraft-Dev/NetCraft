using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//IEntityInsideBehaviour, a block's response to an entity entering, maps to vanilla BlockBehaviour.entityInside
//After an entity moves each tick, the level dispatches to each block its bounding box covers
//Players are not in the entity manager; the level includes the player's bounding box when dispatching
public interface IEntityInsideBehaviour
{
    //OnEntityInside, an entity entered this block; it may be called once per entity in the same tick
    void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state);
}
