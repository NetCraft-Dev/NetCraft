using NetCraft.Nbt;

namespace NetCraft.Storage.Chunk;

//PackedTicks stub对应原版ChunkAccess.PackedTicks
//stub化为List<CompoundTag>保留ticks原始数据不解析
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
