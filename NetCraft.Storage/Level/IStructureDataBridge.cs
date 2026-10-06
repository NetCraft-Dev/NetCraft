using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//IStructureDataBridge, bridge between structure data and chunk storage, implemented by the Game layer
//Only the Game layer knows the concrete structure piece types; the Storage layer just moves the NBT of the structures section
//Maps to vanilla LevelChunk holding structureStarts/structureReferences itself
public interface IStructureDataBridge
{
    //Pack packages the chunk's placements and cross-chunk references into the structures section
    //This NBT is what gets written to disk; the structure content holds no game objects
    CompoundTag Pack(ChunkPos pos);

    //Restore applies the structures section read from disk back into the structure table; unknown structure names are skipped by the implementation
    void Restore(ChunkPos pos, CompoundTag tag);
}
