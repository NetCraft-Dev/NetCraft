using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//BlockEntity block entity base class, maps to vanilla net.minecraft.world.level.block.entity.BlockEntity
//Carries a block's own mutable state (container contents, furnace progress, etc.), ticked and broadcast in one place by BlockEntityManager
//The type comes from BlockEntityType; the registry key is used for persistence, the network id for synchronization
public abstract class BlockEntity
{
    protected BlockEntity(BlockEntityType type, BlockPos pos)
    {
        Type = type;
        Pos = pos;
    }

    //Type block entity type; both the registry key and network id come from it
    public BlockEntityType Type { get; }

    //TypeId network id; sync packets identify the type by it, maps to the vanilla BLOCK_ENTITY_TYPE registry id
    public int TypeId => Type.RawId;

    public BlockPos Pos { get; }

    //Level the owning level, injected by BlockEntityManager when added
    public ServerLevel? Level { get; internal set; }

    //Tick per-tick hook, no-op by default
    public virtual void Tick() { }

    //SaveAdditional writes extra state to NBT, nothing by default
    public virtual void SaveAdditional(CompoundTag tag) { }

    //LoadAdditional reads extra state back from NBT, no-op by default
    public virtual void LoadAdditional(CompoundTag tag) { }

    //OnRemoved callback before the block entity is removed from the world, maps to vanilla preRemoveSideEffects, no-op by default
    public virtual void OnRemoved() { }

    //GetUpdatePacket builds the sync packet, re-serializing the current state on each send
    public ClientboundBlockEntityDataPacket GetUpdatePacket()
    {
        var tag = new CompoundTag();
        SaveAdditional(tag);
        return new ClientboundBlockEntityDataPacket(Pos, TypeId, tag);
    }

    //SaveWithFullMetadata writes the full NBT including type and position, maps to vanilla saveWithFullMetadata
    //id is the registry key, used to look up the type when loading and by data get block; order matches vanilla (id before x/y/z)
    public CompoundTag SaveWithFullMetadata()
    {
        var tag = new CompoundTag();
        tag.PutString("id", Type.Id.ToString());
        tag.PutInt("x", Pos.X);
        tag.PutInt("y", Pos.Y);
        tag.PutInt("z", Pos.Z);
        SaveAdditional(tag);
        return tag;
    }

    //LoadCustomOnly reads back only its own state; position and type metadata are maintained by the level and not included, maps to vanilla loadWithComponents
    public void LoadCustomOnly(CompoundTag tag) => LoadAdditional(tag);
}
