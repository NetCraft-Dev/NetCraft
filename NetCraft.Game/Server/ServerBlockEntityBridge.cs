using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//ServerBlockEntityBridge the Game-layer implementation of the block entity bridge
//Connects the block entity collection and block entity type registry to the Storage layer's chunk storage
public sealed class ServerBlockEntityBridge(ServerLevel level, BlockEntityManager blockEntities)
    : IBlockEntityBridge
{
    //Collect collects the full NBT of the chunk's block entities
    //SaveWithFullMetadata carries the id and position, used on load to resolve the type and restore position
    public List<CompoundTag> Collect(ChunkPos pos)
    {
        var tags = new List<CompoundTag>();
        foreach (var entity in blockEntities.InChunk(pos))
            tags.Add(entity.SaveWithFullMetadata());
        return tags;
    }

    //Restore resolves the type by id and restores the block entity; an unknown id is skipped with a warning
    //Vanilla likewise discards unknown block entities; erroring would make the whole save unreadable
    public void Restore(ChunkPos pos, List<CompoundTag> tags)
    {
        foreach (var tag in tags)
        {
            var entity = BlockEntityTypes.Load(tag);
            if (entity is null)
            {
                Log.Warning($"Block entity type in chunk {pos} is not registered, skipped id={tag.GetStringValue("id")}");
                continue;
            }
            blockEntities.Add(level, entity);
        }
    }

    //Unload clears the chunk's block entities when the chunk unloads
    public void Unload(ChunkPos pos)
    {
        var removed = blockEntities.RemoveInChunk(pos);
        if (removed > 0) Log.Debug($"Chunk unload cleaned up block entities at {pos}, {removed} in total");
    }
}
