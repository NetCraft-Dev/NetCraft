using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//BlockEntityType block entity type, maps to vanilla net.minecraft.world.level.block.entity.BlockEntityType
//Each singleton carries three things: registry key (persisted id field), network id (sync packets and client dispatch), instance factory (placement and load restore)
//Implements the Registry-layer BlockEntityType<object> marker interface; the registry holds it weakly typed, same scheme as entity types
public abstract class BlockEntityType : BlockEntityType<object>
{
    //Id registry key written to the id field on save; vanilla looks up the type by it
    public abstract Identifier Id { get; }

    //RawId network id, aligns with the static declaration order of vanilla BlockEntityTypes; the client dispatches by it
    public abstract int RawId { get; }

    //Create builds an instance at a position; both block placement and load restore go through here
    public abstract BlockEntity Create(BlockPos pos);
}
