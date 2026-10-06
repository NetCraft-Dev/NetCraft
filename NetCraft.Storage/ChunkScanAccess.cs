using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//Chunk scan interface, maps to vanilla ChunkScanAccess
//Streams a chunk through a visitor without building a full Tag, for blending and similar scans
public interface ChunkScanAccess
{
    Task ScanChunk(ChunkPos pos, StreamTagVisitor visitor);
}
