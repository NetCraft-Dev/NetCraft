using NetCraft.Nbt;

namespace NetCraft.Storage.Chunk;

//PackedTicks stub, maps to vanilla ChunkAccess.PackedTicks
//Stubbed as List<CompoundTag>, keeping the raw tick data without parsing
public sealed class PackedTicks
{
    public List<CompoundTag> Blocks { get; } = new();
    public List<CompoundTag> Fluids { get; } = new();

    public PackedTicks() { }

    public PackedTicks(List<CompoundTag> blocks, List<CompoundTag> fluids)
    {
        Blocks = blocks;
        Fluids = fluids;
    }
}
