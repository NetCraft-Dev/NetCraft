using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//IBlockEntityBridge, bridge between block entities and chunk storage, implemented by the Game layer
//Only the Game layer knows the concrete block entity types; the Storage layer just moves NBT
//Collect for disk writes, restore on load and cleanup on chunk unload all go through it; maps to vanilla LevelChunk holding blockEntities itself
public interface IBlockEntityBridge
{
    //Collect returns the full NBT of all block entities in the chunk (including id and coord triplet), for disk writes and chunk packets
    List<CompoundTag> Collect(ChunkPos pos);

    //Restore applies the block entity NBT read from disk back into the level; unknown ids are skipped by the implementation
    void Restore(ChunkPos pos, List<CompoundTag> tags);

    //Unload clears the chunk's block entities on chunk unload; maps to vanilla block entity cleanup on chunk unload
    void Unload(ChunkPos pos);
}
