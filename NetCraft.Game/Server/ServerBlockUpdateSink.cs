using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.Server;

//ServerBlockUpdateSink the Game-layer side-effect implementation of the update chain, attached to the level at server assembly
//The block entity container and drop flow are in the Game layer; the update chain only calls back here at the right moment
public sealed class ServerBlockUpdateSink : IBlockUpdateSink
{
    private readonly PersistentServerLevel _level;
    private readonly PlayerList _players;
    private readonly BlockEntityManager _blockEntities;

    //Block positions changed this tick but not yet dispatched, deduplicated by position, maps to vanilla changedBlocksPerSection per chunk
    private readonly HashSet<BlockPos> _pendingBlockUpdates = new();

    public ServerBlockUpdateSink(PersistentServerLevel level, PlayerList players, BlockEntityManager blockEntities)
    {
        _level = level;
        _players = players;
        _blockEntities = blockEntities;
    }

    //RemoveBlockEntity removes a block entity
    //No extra empty packet is broadcast: the client handleBlockEntityData is getBlockEntity(pos, type).ifPresent(...)
    //Passing type 0 would hit a block entity like the first registry entry on the client and overwrite its state with an empty tag
    //And client-side block entity removal is done by the subsequent block update anyway (a new block with no block entity clears the old)
    public bool RemoveBlockEntity(BlockPos pos)
    {
        //Give the block entity a finishing chance before removal; pistons use it to land an unfinished animation
        if (_blockEntities.Get(pos) is { } entity) entity.OnRemoved();
        return _blockEntities.Remove(pos);
    }

    //DestroyBlock runs the full destroy flow, used when the shape update computes air, maps to vanilla Level.destroyBlock
    public void DestroyBlock(BlockPos pos, bool dropItems, int updateLimit)
        => ServerBlockUpdates.DestroyBlock(_level, _players, pos, dropItems);

    //BlockChanged records the blocks changed this tick, maps to vanilla entering ChunkHolder.changedBlocksPerSection when flags include UPDATE_CLIENTS
    public void BlockChanged(BlockPos pos, BlockState state) => _pendingBlockUpdates.Add(pos);

    //FlushBlockUpdates sends the accumulated block changes to clients, maps to vanilla ChunkHolder.broadcastChanges
    //On broadcast the current state at the position is read; a cell changed multiple times in one tick sends only the last
    public void FlushBlockUpdates()
    {
        if (_pendingBlockUpdates.Count == 0) return;
        foreach (var pos in _pendingBlockUpdates)
        {
            var state = _level.GetBlockState(pos);
            if (state is not { } current) continue;
            //The dispatch sequence is the first-hand basis for debugging client behavior; without it there is only guesswork
            Log.Debug($"[Send] block update {pos} -> {current.Owner.Id}");
            _players.BroadcastAll(new ClientboundBlockUpdatePacket(pos, current.Id));
        }
        _pendingBlockUpdates.Clear();
    }

    //LevelEvent broadcasts a world event, maps to vanilla Level.levelEvent
    public void LevelEvent(int kind, BlockPos pos, int data)
        => _players.BroadcastAll(new ClientboundLevelEventPacket(kind, pos, data, false));

    //BlockEvent broadcasts a block event, maps to the broadcast after a successful trigger in vanilla Level.runBlockEvents
    //The packet's blockId is the block registry index; the client finds its own block by it and runs triggerEvent again
    public void BlockEvent(BlockPos pos, NetCraft.Registry.Block block, int paramA, int paramB)
    {
        var blockId = BuiltInRegistries.BLOCK.GetId(block);
        if (blockId < 0) return;
        //The block event packet drives the client's replay of piston movement; without it only the final result remains
        Log.Debug($"[Send] block event {pos} {block.Id.Path} b0={paramA} b1={paramB} id={blockId}");
        _players.BroadcastAll(new ClientboundBlockEventPacket(pos, (byte)paramA, (byte)paramB, blockId));
    }

    //AddBlockEntity creates a block entity for a new block, maps to newBlockEntity in vanilla LevelChunk
    //Blocks with no block entity are skipped
    public void AddBlockEntity(BlockPos pos, BlockState state)
    {
        if (state.Owner is not BlockBehaviour behaviour) return;
        if (behaviour.CreateBlockEntity(pos, state) is not { } entity) return;
        _blockEntities.Add(_level, entity);
        _players.BroadcastAll(entity.GetUpdatePacket());
    }

    //GetBlockEntity gets the block entity; comparators and the like need to read their own mutable state
    public object? GetBlockEntity(BlockPos pos) => _blockEntities.Get(pos);

    //BlockEntityChanged syncs the client after block entity data changes
    public void BlockEntityChanged(BlockPos pos)
    {
        if (_blockEntities.Get(pos) is { } entity) _players.BroadcastAll(entity.GetUpdatePacket());
    }

    //SetBlockEntity registers an already-built block entity and sends its current state to the client once
    public void SetBlockEntity(object entity)
    {
        if (entity is not BlockEntity blockEntity) return;
        _blockEntities.Add(_level, blockEntity);
        //The moving piston state goes over in this packet; the client positions the pushed block model from it
        Log.Debug($"[Send] block entity {blockEntity.Pos} {blockEntity.Type.Id}");
        _players.BroadcastAll(blockEntity.GetUpdatePacket());
    }

    //PlaySound plays a positional sound to all online players
    public void PlaySound(SoundEvent sound, SoundSource source, double x, double y, double z,
        float volume, float pitch)
        => ServerSounds.PlaySound(_players, sound, source, x, y, z, volume, pitch);
}
