using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//IBlockUpdateSink, the Game-layer side-effect sink of the update chain, injected by the Game layer
//Block entity containers and drop broadcasts are Game-layer duties; Storage only calls them at the right time
public interface IBlockUpdateSink
{
    //RemoveBlockEntity removes the block entity at that pos, returns whether an old one was actually removed
    //Maps to removeBlockEntity inside vanilla LevelChunk.setBlockState
    bool RemoveBlockEntity(BlockPos pos);

    //DestroyBlock runs the full destroy flow, handling drops and block entity contents together
    //Used when the shape update yields air, maps to vanilla Level.destroyBlock
    void DestroyBlock(BlockPos pos, bool dropItems, int updateLimit);

    //BlockChanged syncs a block change to clients, maps to UPDATE_CLIENTS in the vanilla flags
    //Redstone components changing their own state in behavior callbacks broadcast through this sink; otherwise the client never sees the change
    void BlockChanged(BlockPos pos, BlockState state);

    //FlushBlockUpdates sends out this tick's accumulated block changes, maps to vanilla ChunkHolder.broadcastChanges
    //Vanilla records block updates and defers sending them to the chunk tick stage, while block event packets are sent immediately
    //This ordering is what lets piston movement replay on the client; when the client replays, the cell ahead must still be the original block
    void FlushBlockUpdates();

    //LevelEvent broadcasts a world event, maps to vanilla Level.levelEvent
    //Purely cosmetic events like a redstone torch burning out go through it; block behavior does not hold the player list directly
    void LevelEvent(int kind, BlockPos pos, int data);

    //BlockEvent broadcasts a block event, maps to the broadcast after a successful triggerEvent in vanilla Level.runBlockEvents
    //Piston motion and chest opening rely on it visually; if the server computes the event but never sends it, the client stays in the old state
    void BlockEvent(BlockPos pos, NetCraft.Registry.Block block, int paramA, int paramB);

    //AddBlockEntity creates a block entity for a newly written block, maps to newBlockEntity in vanilla LevelChunk
    //The block entity container is in the Game layer; Storage only signals at the right time
    void AddBlockEntity(BlockPos pos, BlockState state);

    //GetBlockEntity gets the block entity at that pos, used by block behavior to read its own mutable state; returns null when absent
    //Returns object because the block entity type lives in the Game layer and Storage does not know it
    object? GetBlockEntity(BlockPos pos);

    //BlockEntityChanged signals that block entity data changed and must reach the client
    void BlockEntityChanged(BlockPos pos);

    //SetBlockEntity puts in an already-built block entity, maps to vanilla Level.setBlockEntity
    //When a piston pushes, the block entity must carry the moved flag and motion params, so it cannot go through the create-by-state path
    //entity is passed as object since the block entity type lives in the Game layer and Storage does not know it
    void SetBlockEntity(object entity);

    //PlaySound plays a sound at the given coords, maps to vanilla Level.playSound
    //Block behavior does not hold the player list; sounds are broadcast by the Game-layer sink
    void PlaySound(SoundEvent sound, SoundSource source, double x, double y, double z,
        float volume, float pitch);
}
