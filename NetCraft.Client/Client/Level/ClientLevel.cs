using System.Collections.Concurrent;
using System.Threading;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
//Attribute map types carry their own namespace; only these two names are taken here
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
//The default entity attribute table is under the entity namespace; only this one type is used
using DefaultAttributes = NetCraft.Game.World.Entity.DefaultAttributes;

namespace NetCraft.Game.Client.Level;

//ClientLevel client world, maps to vanilla net.minecraft.client.multiplayer.ClientLevel
//Holds chunk storage + light storage, providing BlockState/BlockLight/SkyLight queries
//Light is stored separately, not attached to LevelChunkSection, aligning with vanilla LevelLightEngine's design
//W8 added a ReaderWriterLockSlim to protect the section's internal PalettedContainer; SetBlockState takes the write lock, dispatcher Build takes the read lock
//_chunks/_lights use ConcurrentDictionary for dictionary-level thread safety; LoadChunk/UnloadChunk take no extra lock to avoid blocking compilation
//SectionDirty fires outside the write lock to prevent a deadlock from reading ClientLevel inside the callback; dispatcher subscribes and calls MarkDirty to spread
public sealed class ClientLevel
{
    //Chunk storage indexed by ChunkPos, ConcurrentDictionary dictionary-level thread safety
    private readonly ConcurrentDictionary<ChunkPos, ChunkAccess> _chunks = new();
    //Light storage indexed by SectionPos.AsLong, ConcurrentDictionary dictionary-level thread safety
    private readonly ConcurrentDictionary<long, (DataLayer BlockLight, DataLayer SkyLight)> _lights = new();
    //Block entity state indexed by BlockPos.AsLong; a local copy of the server's ClientboundBlockEntityData
    private readonly ConcurrentDictionary<long, CompoundTag> _blockEntities = new();
    //Moving blocks indexed by BlockPos.AsLong, parsed from block entity state for the render layer's advance animation
    private readonly ConcurrentDictionary<long, ClientMovingBlock> _movingBlocks = new();
    //Entity table indexed by entity id; a local copy from server entity packets
    private readonly ConcurrentDictionary<int, ClientEntity> _entities = new();
    //_syncRoot protects the section's internal PalettedContainer; SetBlockState takes the write lock, dispatcher Build takes the read lock
    private readonly ReaderWriterLockSlim _syncRoot = new();
    //_tickCount client world accumulated ticks; does not advance while frozen. Entity animation derives age in ticks from it
    private long _tickCount;
    //_isFrozen tick-rate frozen state sent by the server; while frozen the local world stops advancing
    private bool _isFrozen;
    //_stepTicksToRun pending step ticks while frozen, written when the server sends a step packet; after these ticks it returns to frozen
    private int _stepTicksToRun;
    //_tickRate ticks per second sent by the server; the local world advances ticks by it, maps to vanilla TickRateManager.tickRate
    //Without this, after slowing /tick rate the client would still advance at 20tps on its own, making tick-based animations like pistons tens of times faster than the server
    private float _tickRate = DefaultTickRate;
    //_tickAccumulator seconds accumulated toward the next tick; used to make up the difference when logical frames and tick rate disagree
    private double _tickAccumulator;

    //DefaultTickRate client initial ticks per second, same as the server default
    private const float DefaultTickRate = 20f;

    //TickCount client world accumulated ticks, for the render layer to drive entity animation by tick count
    public long TickCount => _tickCount;

    //IsFrozen whether the world is frozen
    public bool IsFrozen => _isFrozen;

    //SetTickingState records the server-sent tick rate and frozen state, maps to vanilla handling ClientboundTickingStatePacket
    public void SetTickingState(float tickRate, bool frozen)
    {
        _isFrozen = frozen;
        _tickRate = tickRate > 0f ? tickRate : DefaultTickRate;
        if (frozen) return;
        //On unfreeze clear leftover step counts, otherwise the next freeze would get a few free beats
        _stepTicksToRun = 0;
    }

    //SetTickingStep records the server-sent pending step ticks, maps to vanilla handling ClientboundTickingStepPacket
    public void SetTickingStep(int tickSteps) => _stepTicksToRun = tickSteps;

    //Tick the client world advances by real elapsed time; frozen stops it and entity animation stops changing
    //Maps to vanilla TickRateManager.tick: each accumulated tick first decides whether this beat advances, then decrements the pending step ticks
    //deltaSeconds defaults to the default tick length; call sites without frame rate info treat each beat as one tick
    public void Tick(double deltaSeconds = 1.0 / DefaultTickRate)
    {
        //While frozen with no pending step ticks it neither advances nor accumulates time, otherwise the unfreeze frame would catch up all ticks owed during the freeze at once
        if (_isFrozen && _stepTicksToRun <= 0)
        {
            _tickAccumulator = 0;
            return;
        }
        _tickAccumulator += deltaSeconds;
        var interval = 1.0 / _tickRate;
        //At most 128 ticks per frame, to prevent a frame-rate hiccup or long suspension from catching up all at once and stalling the main thread
        for (var step = 0; step < 128 && _tickAccumulator >= interval; step++)
        {
            _tickAccumulator -= interval;
            if (_stepTicksToRun > 0) _stepTicksToRun--;
            _tickCount++;
            //A moving block's advance progress is based on the world's elapsed ticks; each step counts one tick
            if (!_movingBlocks.IsEmpty)
                foreach (var moving in _movingBlocks.Values) moving.Advance();
            //When the step ticks run out it returns to frozen immediately; the leftover fraction is discarded to avoid a free beat next freeze
            if (_isFrozen && _stepTicksToRun <= 0)
            {
                _tickAccumulator = 0;
                return;
            }
        }
    }

    //SectionDirty block change event fired outside the write lock; dispatcher subscribes and calls MarkDirty to spread to itself + 6 neighbors
    public event Action<SectionPos>? SectionDirty;

    //ChunkUnloaded chunk unload event, fired once per section after a successful TryRemove
    //The dispatcher subscribes and calls UnloadSection to clean up RenderSection and prevent leaks
    public event Action<SectionPos>? ChunkUnloaded;

    //EnterReadLock/ExitReadLock let SectionRenderDispatcher hold the read lock while calling Build, preventing SetBlockState data races
    //While holding the read lock, concurrent reads are allowed and SetBlockState's write lock blocks; RecursionPolicy.NoRecursion means read methods that lock internally must not be called recursively
    public void EnterReadLock() => _syncRoot.EnterReadLock();
    public void ExitReadLock() => _syncRoot.ExitReadLock();

    //LoadChunk loads a chunk, overwriting the old chunk at the same position; ConcurrentDictionary's indexer is thread-safe, no write lock needed
    public void LoadChunk(ChunkAccess chunk) => _chunks[chunk.Pos] = chunk;

    //LoadLight loads section light, overwriting the old light at the same position; ConcurrentDictionary's indexer is thread-safe
    //On a chunk's first load both layers arrive together; still mark dirty after loading, otherwise the mesh uses data without light
    public void LoadLight(SectionPos pos, DataLayer blockLight, DataLayer skyLight)
    {
        _lights[pos.AsLong()] = (blockLight, skyLight);
        SectionDirty?.Invoke(pos);
    }

    //SetLightLayer updates only one light layer while keeping the other; when missing it fills in an empty layer
    //Light packets arrive incrementally per layer, with sky and block light arriving separately; LoadLight must not overwrite the whole thing or the layer that arrived first is cleared
    //Light is baked into the section mesh; after writing it must notify the render layer to rebuild, otherwise it only refreshes on the next block change
    public void SetLightLayer(SectionPos pos, bool skyLayer, DataLayer layer)
    {
        var key = pos.AsLong();
        if (!_lights.TryGetValue(key, out var current))
            current = (new DataLayer(), new DataLayer());
        _lights[key] = skyLayer ? (current.BlockLight, layer) : (layer, current.SkyLight);
        SectionDirty?.Invoke(pos);
    }

    //SetBlockEntityData records the server-sent block entity state; empty NBT means the block entity there was removed
    //Moving block state packets are parsed out along the way; the render layer uses them to compute per-frame advance displacement
    public void SetBlockEntityData(BlockPos pos, CompoundTag? tag)
    {
        if (tag is null)
        {
            _blockEntities.TryRemove(pos.AsLong(), out _);
            _movingBlocks.TryRemove(pos.AsLong(), out _);
            return;
        }
        _blockEntities[pos.AsLong()] = tag;
        //Anything with a blockState tag is a moving block; an air id of 0 creates no record
        if (tag.GetIntOr("blockState", -1) > 0)
            _movingBlocks[pos.AsLong()] = new ClientMovingBlock(pos,
                BlockStateRegistry.GetState(tag.GetIntOr("blockState", 0)),
                Direction.ById(tag.GetIntOr("facing", 0)),
                tag.GetBooleanOr("extending", false),
                tag.GetBooleanOr("source", false));
    }

    //MovingBlocks read-only view of client moving blocks for the render layer to iterate
    public IReadOnlyCollection<ClientMovingBlock> MovingBlocks => _movingBlocks.Values.ToArray();

    //ForgetMovingBlock clears the local record when the position is no longer a moving block
    //When the server replaces moving_piston with a real block it does not resend an empty block entity packet; this path finishes it up via the block update
    public void ForgetMovingBlock(BlockPos pos) => _movingBlocks.TryRemove(pos.AsLong(), out _);

    //TryGetBlockEntityData gets the block entity state for the render layer to read
    public bool TryGetBlockEntityData(BlockPos pos, out CompoundTag? tag)
        => _blockEntities.TryGetValue(pos.AsLong(), out tag);

    //AddEntity records a server-sent entity, overwriting the same id
    //The attribute table defaults by entity type; the client has initial attributes locally and the server only resends when attributes were changed
    public void AddEntity(int id, EntityType<object> type, Vec3 pos, float yRot, float xRot, Vec3 velocity, bool onGround)
        => _entities[id] = new ClientEntity(type, pos, yRot, xRot, velocity, onGround)
        {
            Attributes = new AttributeMap(DefaultAttributes.GetSupplier(type) ?? AttributeSupplier.Empty),
            //The bob phase takes a random value, corresponding to bobOffs in the vanilla client entity constructor; this value is not synced by the server
            BobOffset = Random.Shared.NextSingle() * MathF.PI * 2f,
            SpawnedAtTick = _tickCount,
        };

    //RemoveEntities removes entities the server deleted
    public void RemoveEntities(int[] ids)
    {
        foreach (var id in ids) _entities.TryRemove(id, out _);
    }

    //MoveEntity updates an entity by relative displacement and orientation; displacement is in 1/4096 block units and a null angle means that axis is unchanged
    public void MoveEntity(int id, double dx, double dy, double dz, float? yRot, float? xRot, bool onGround)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        _entities[id] = entity with
        {
            Pos = new Vec3(entity.Pos.X + dx, entity.Pos.Y + dy, entity.Pos.Z + dz),
            YRot = yRot ?? entity.YRot,
            XRot = xRot ?? entity.XRot,
            OnGround = onGround,
        };
    }

    //SetEntityPosition teleport packet overwrites the entity position and orientation entirely
    public void SetEntityPosition(int id, Vec3 pos, float yRot, float xRot, bool onGround)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        _entities[id] = entity with { Pos = pos, YRot = yRot, XRot = xRot, OnGround = onGround };
    }

    //SetEntityMotion records the entity velocity for interpolation
    public void SetEntityMotion(int id, Vec3 velocity)
    {
        if (_entities.TryGetValue(id, out var entity))
            _entities[id] = entity with { Velocity = velocity };
    }

    //SetEntityData updates entity metadata, overwriting entry by entry by index
    //The dictionary lives on the record instance and is modified in place without rebuilding the record, avoiding clobbering updates to other fields
    public void SetEntityData(int id, IReadOnlyList<EntityDataItem> items)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        foreach (var item in items) entity.Data[item.Index] = item.Value;
    }

    //SetEntityAttributes applies the server-sent attribute snapshots, maps to vanilla handleUpdateAttributes
    //Each entry overwrites the base value then replaces modifiers wholesale as in vanilla; the client table is open and any received attribute can land in it
    public void SetEntityAttributes(int id, IReadOnlyList<AttributeSnapshot> snapshots)
    {
        if (!_entities.TryGetValue(id, out var entity)) return;
        foreach (var snapshot in snapshots)
        {
            var instance = entity.Attributes.GetInstance(snapshot.Attribute);
            if (instance is null) continue;
            instance.SetBaseValue(snapshot.Base);
            foreach (var modifier in instance.Modifiers) instance.RemoveModifier(modifier.Id);
            foreach (var modifier in snapshot.Modifiers) instance.AddTransientModifier(modifier);
        }
    }

    //TryGetEntity gets a client entity copy
    public bool TryGetEntity(int id, out ClientEntity? entity)
        => _entities.TryGetValue(id, out entity);

    //Entities read-only view of client entities for the render layer to iterate
    public IReadOnlyCollection<ClientEntity> Entities => _entities.Values.ToArray();

    //UnloadChunk removes the chunk and its associated light, iterating all sectionY of that chunk
    //ConcurrentDictionary.Keys is a snapshot so traversal is safe and removal is thread-safe
    //After a successful TryRemove, fires ChunkUnloaded once per section to notify the render layer to clean up
    public void UnloadChunk(ChunkPos pos)
    {
        if (!_chunks.TryRemove(pos, out var chunk)) return;
        var keysToRemove = new List<long>();
        foreach (var key in _lights.Keys)
        {
            var sx = SectionPos.GetX(key);
            var sz = SectionPos.GetZ(key);
            if (sx == pos.X && sz == pos.Z) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove) _lights.TryRemove(key, out _);
        //Block entity state in the same chunk is swept too; the key is a packed BlockPos
        foreach (var key in _blockEntities.Keys)
        {
            if ((BlockPos.GetX(key) >> 4) == pos.X && (BlockPos.GetZ(key) >> 4) == pos.Z)
                _blockEntities.TryRemove(key, out _);
        }
        //Events fire after removal; GetSection inside the callback already returns null and there is no deadlock
        for (var y = chunk.MinSectionY; y <= chunk.MaxSectionY; y++)
            ChunkUnloaded?.Invoke(new SectionPos(pos.X, y, pos.Z));
    }

    //GetBlockState looks up the block state at world coordinates; out of range or not loaded returns default (air)
    //The caller holds the read lock to prevent SetBlockState data races; the section internals are not thread-safe
    public BlockState GetBlockState(BlockPos pos)
    {
        var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return default;
        var section = chunk.GetSection(pos.Y >> 4);
        if (section is null) return default;
        return section.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetBlockLight looks up block light at world coordinates; out of range returns 0
    public int GetBlockLight(BlockPos pos) => GetLight(pos, true);

    //GetSkyLight looks up sky light at world coordinates; out of range returns 15
    public int GetSkyLight(BlockPos pos) => GetLight(pos, false);

    //GetLight internal light query; blockLight=true takes BlockLight, otherwise SkyLight
    //For out-of-range sections block=0 sky=15, aligning with vanilla default behavior
    private int GetLight(BlockPos pos, bool blockLight)
    {
        var sectionPos = SectionPos.Of(pos);
        if (!_lights.TryGetValue(sectionPos.AsLong(), out var light))
            return blockLight ? 0 : 15;
        var layer = blockLight ? light.BlockLight : light.SkyLight;
        return layer.Get(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetSection gets the LevelChunkSection by section coordinates; not loaded returns null
    //The caller holds the read lock to prevent SetBlockState data races
    public LevelChunkSection? GetSection(int sectionX, int sectionY, int sectionZ)
    {
        var chunkPos = new ChunkPos(sectionX, sectionZ);
        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return null;
        return chunk.GetSection(sectionY);
    }

    //HasChunk whether the chunk is loaded; ConcurrentDictionary is thread-safe
    public bool HasChunk(ChunkPos pos) => _chunks.ContainsKey(pos);

    //GetLoadedChunks returns a snapshot of all loaded chunks for LevelRenderer/ViewArea to iterate
    //ConcurrentDictionary.Values returns a snapshot so traversal is safe; the caller need not hold a lock
    public IEnumerable<ChunkAccess> GetLoadedChunks() => _chunks.Values;

    //SetBlockState changes a block, taking the write lock to protect the section's internal PalettedContainer and firing SectionDirty outside the write lock
    //Returns false without firing the event when the chunk or section is not loaded
    public bool SetBlockState(BlockPos pos, BlockState state)
    {
        _syncRoot.EnterWriteLock();
        try
        {
            var chunkPos = new ChunkPos(pos.X >> 4, pos.Z >> 4);
            if (!_chunks.TryGetValue(chunkPos, out var chunk)) return false;
            var section = chunk.GetSection(pos.Y >> 4);
            if (section is null) return false;
            section.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        }
        finally { _syncRoot.ExitWriteLock(); }
        //Replacing with another block means the move at this cell is done, so the local moving block record becomes void
        if (state.Owner.Id.Path != "moving_piston") ForgetMovingBlock(pos);
        //Fire the event outside the write lock to prevent a deadlock from reading ClientLevel inside the callback; dispatcher.MarkDirty spreads to itself + 6 neighbors
        SectionDirty?.Invoke(SectionPos.Of(pos));
        return true;
    }

    //ClientEntity client entity state keeps only the fields needed for rendering and interpolation
    //Data entity metadata stored by index; the dropped item's item stack is at ItemEntity.DataItemIndex
    public sealed record ClientEntity(EntityType<object> Type, Vec3 Pos, float YRot, float XRot, Vec3 Velocity,
        bool OnGround)
    {
        //Data entity metadata, index-to-value mapping
        public Dictionary<byte, object> Data { get; init; } = new();

        //Attributes entity attribute table; defaults by entity type and is overridden in AddEntity; empty when unspecified
        public AttributeMap Attributes { get; init; } = new(AttributeSupplier.Empty);

        //BobOffset bobbing phase, randomly chosen when the entity is added, corresponding to bobOffs in the vanilla client entity constructor
        public float BobOffset { get; init; }

        //SpawnedAtTick the world tick when the client received the entity; subtracting it from the current tick gives the age in ticks driving animation
        //Using ticks instead of wall clock keeps animation from advancing while the world is frozen
        public long SpawnedAtTick { get; init; }

        //DroppedItem the item stack a dropped item holds; null for non-drops or before metadata arrives
        public ItemStack? DroppedItem
            => Data.TryGetValue(World.Entity.ItemEntity.DataItemIndex, out var value) ? value as ItemStack : null;
    }

    //ClientMovingBlock client-side moving block for one cell, corresponding to the vanilla client's PistonMovingBlockEntity
    //The server sends the state packet once on the piston move; afterwards the client derives progress from the world's elapsed ticks
    //TicksAlive how many ticks the world has advanced since the state packet; counting ticks rather than timestamps avoids the packet's arrival frame being over-counted by half a block
    public sealed class ClientMovingBlock
    {
        public ClientMovingBlock(BlockPos pos, BlockState movedState, Direction direction, bool extending,
            bool isSource)
        {
            Pos = pos;
            MovedState = movedState;
            Direction = direction;
            Extending = extending;
            IsSource = isSource;
        }

        public BlockPos Pos { get; }
        public BlockState MovedState { get; }
        public Direction Direction { get; }
        public bool Extending { get; }
        public bool IsSource { get; }

        //TicksAlive elapsed ticks; two ticks finish the move and more has no effect
        public int TicksAlive { get; private set; }

        //Progress current advance progress 0 to 1; half a block per tick, done in two ticks, maps to vanilla PistonMovingBlockEntity.getProgress
        public float Progress => MathF.Min(1f, TicksAlive * 0.5f);

        //Advance called when the world advances one tick
        public void Advance()
        {
            if (TicksAlive < 2) TicksAlive++;
        }

        //RenderState what is actually drawn at this cell during the move
        //When retracting, the movedState of the piston section is the piston base; drawing it as-is would show a whole piston block sliding back
        //Vanilla draws the piston head at this cell, same as PistonMovingBlockEntity.getCollisionRelatedBlockState
        public BlockState RenderState
        {
            get
            {
                if (Extending || !IsSource) return MovedState;
                if (MovedState.Owner.Id.Path is not ("piston" or "sticky_piston")) return MovedState;
                return NetCraft.Game.World.Level.Block.Blocks.PISTON_HEAD.DefaultBlockState
                    .SetValue(BlockStateProperties.Short, Progress > 0.25f)
                    .SetValue(BlockStateProperties.PistonTypeProperty,
                        MovedState.Owner.Id.Path == "sticky_piston"
                            ? NetCraft.Registry.Enums.PistonType.sticky
                            : NetCraft.Registry.Enums.PistonType.normal)
                    .SetValue(BlockStateProperties.FacingProperty,
                        MovedState.GetValue(BlockStateProperties.FacingProperty));
            }
        }
    }
}
