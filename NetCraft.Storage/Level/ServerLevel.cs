using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Light;
using NetCraft.Storage.Redstone;
using NetCraft.Storage.Ticks;
using NetCraft.Storage.Updates;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Storage;

//ServerLevel, server level abstract class, maps to vanilla net.minecraft.world.level.ServerLevel
//Holds the dimension identifier and the level data access entry point; the block update chain also lives here as the single entry point for all world changes
//Implements ILevelReader, exposing the set of reads the predicate layer needs to the Registry side
public abstract class ServerLevel : ILevelReader
{
    //Built lazily; it cannot be built during construction before the instance is ready
    private INeighborUpdater? _neighborUpdater;

    //Dimension, the dimension registry name such as the nether/end
    public abstract Identifier Dimension { get; }

    //DataVersion, the level data version used by the DataFixer upgrade check
    public abstract int DataVersion { get; }

    //RegistryAccess, the registry access entry point used by Codec parsing for lookups
    //Subclasses provide the concrete RegistryAccess instance, maps to vanilla serverLevel.registryAccess()
    public abstract RegistryAccess RegistryAccess { get; }

    //GetChunk gets the chunk access instance by ChunkPos, maps to vanilla getChunk
    //Returns null when the chunk is not loaded; subclasses provide the concrete load logic
    public abstract ChunkAccess? GetChunk(ChunkPos pos);

    //GetNextEntityId allocates the next available entity id, maps to vanilla ServerLevel.getNextEntityId
    //No dedup by default; only persistent levels know which entities they hold, subclasses override to wire in the occupancy check
    public virtual int GetNextEntityId() => NetCraft.Registry.Entity.NextEntityId(_ => false);

    //GameTime, the total world game time
    public long GameTime { get; set; }

    //WorldBorder, world border; the server assembles a save instance, and without it this is the default max-size border
    public WorldBorder WorldBorder { get; set; } = new();

    //MinBuildHeight, the dimension's lowest placeable height; defaults to the vanilla overworld value, subclasses override with the real sections
    public virtual int MinBuildHeight => -64;

    //MaxBuildHeight, the exclusive upper bound of the dimension's placeable height; defaults to the vanilla overworld value
    public virtual int MaxBuildHeight => 320;

    //ORainLevel, the previous tick's rain level, paired with RainLevel for render interpolation, maps to vanilla oRainLevel
    public float ORainLevel { get; set; }

    //RainLevel, the current rain level 0-1, easing toward the weather target each tick
    public float RainLevel { get; set; }

    //OThunderLevel, the previous tick's thunder level, maps to vanilla oThunderLevel
    public float OThunderLevel { get; set; }

    //ThunderLevel, the current thunder level 0-1
    public float ThunderLevel { get; set; }

    //GetRainLevel interpolates the rain level by partial tick, maps to vanilla getRainLevel
    public float GetRainLevel(float deltaPartialTick) => Mth.Lerp(deltaPartialTick, ORainLevel, RainLevel);

    //SetRainLevel sets the rain level directly and aligns the previous value, maps to vanilla setRainLevel
    public void SetRainLevel(float rainLevel)
    {
        var clamped = Mth.Clamp(rainLevel, 0.0f, 1.0f);
        ORainLevel = clamped;
        RainLevel = clamped;
    }

    //GetThunderLevel scales the thunder level by the rain level, maps to vanilla getThunderLevel
    public float GetThunderLevel(float deltaPartialTick)
        => Mth.Lerp(deltaPartialTick, OThunderLevel, ThunderLevel) * GetRainLevel(deltaPartialTick);

    //SetThunderLevel sets the thunder level directly and aligns the previous value, maps to vanilla setThunderLevel
    public void SetThunderLevel(float thunderLevel)
    {
        var clamped = Mth.Clamp(thunderLevel, 0.0f, 1.0f);
        OThunderLevel = clamped;
        ThunderLevel = clamped;
    }

    //CanHaveWeather, whether the dimension has weather, maps to vanilla canHaveWeather
    //The vanilla test is no sky light or a ceiling or the end meaning no weather; this project currently has only the overworld as a dimension that can rain
    public virtual bool CanHaveWeather() => true;

    //IsRaining, whether it is raining, threshold 0.2, maps to vanilla isRaining
    public bool IsRaining => CanHaveWeather() && GetRainLevel(1.0f) > 0.2f;

    //IsThundering, whether it is thundering, threshold 0.9, maps to vanilla isThundering
    public bool IsThundering => CanHaveWeather() && GetThunderLevel(1.0f) > 0.9;

    //DayTime, day-night time cycling 0-23999; a legacy field only used as a transition in the command layer
    public long DayTime { get; set; }

    //DefaultClock, the dimension's default clock, maps to vanilla DimensionType.defaultClock, assembled by the server
    public Holder<WorldClock>? DefaultClock { get; set; }

    //NeighborUpdater, the update dispatcher; both the neighbor update and shape update channels queue through it
    public INeighborUpdater NeighborUpdater
        => _neighborUpdater ??= new CollectingNeighborUpdater(this, MaxChainedNeighborUpdates);

    //MaxChainedNeighborUpdates, the chained update limit; a negative value means unlimited
    protected virtual int MaxChainedNeighborUpdates => SharedConstants.MaxChainedNeighborUpdates;

    //BlockUpdateSink, the Game-layer side-effect sink of the update chain; without injection block entity removal and destroy side effects are missing
    public IBlockUpdateSink? BlockUpdateSink { get; set; }

    //BlockEntityBridge, the bridge for saving and cleaning block entities; without injection chunks are written without block entities
    public IBlockEntityBridge? BlockEntityBridge { get; set; }

    //StructureDataBridge, the save bridge for structure placements; without injection chunks are written without a structures section
    public IStructureDataBridge? StructureDataBridge { get; set; }

    //Random, the level random source, used by scheduled and random ticks
    public RandomSource Random { get; set; } = RandomSource.Create();

    //IsHandlingTick, whether a level tick is being processed, maps to vanilla ServerLevel.handlingTick
    //Set true by the server main loop at the start of a level tick and false after block events run
    //Piston retraction checks use it: a retraction signal in the same tick means the push is not done yet and must downgrade to a drop
    public bool IsHandlingTick { get; set; }

    //_subTickCount, the enqueue order within the same tick, maps to vanilla Level.subTickCount
    private long _subTickCount;
    private LevelTicks<NetCraft.Registry.Block>? _blockTicks;
    private LevelTicks<NetCraft.Registry.Fluid>? _fluidTicks;
    //_blockEvents, the block event queue, insertion-ordered and deduplicated, maps to vanilla ServerLevel.blockEvents
    private readonly LinkedList<BlockEventData> _blockEvents = new();
    private readonly HashSet<BlockEventData> _blockEventSet = new();
    private readonly List<BlockEventData> _blockEventsToReschedule = new();
    //_tickRegisteredChunks, chunks with a registered scheduled tick container, avoiding duplicate registration
    private readonly HashSet<long> _tickRegisteredChunks = new();

    //BlockTicks, the block scheduled tick collection, built lazily
    public LevelTicks<NetCraft.Registry.Block> BlockTicks
        => _blockTicks ??= new LevelTicks<NetCraft.Registry.Block>(IsPositionTicking);

    //FluidTicks, the fluid scheduled tick collection, built lazily
    public LevelTicks<NetCraft.Registry.Fluid> FluidTicks
        => _fluidTicks ??= new LevelTicks<NetCraft.Registry.Fluid>(IsPositionTicking);

    //IsPositionTicking, whether the chunk is within the tickable range; all allowed by default, the server narrows it by view distance
    protected virtual bool IsPositionTicking(long chunkKey) => true;

    //EnsureChunkTicksRegistered registers the chunk's scheduled tick container and does nothing when already registered
    //Cannot rely only on the chunk load callback; the path that retrieves a chunk directly through GetChunk does not trigger it
    //Unpack happens on first registration, converting relative delays from disk into absolute ticks against the current game tick
    public void EnsureChunkTicksRegistered(ChunkAccess chunk)
    {
        if (!_tickRegisteredChunks.Add(ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z))) return;
        chunk.BlockTicks.Unpack(GameTime);
        BlockTicks.AddContainer(chunk.Pos, chunk.BlockTicks);
        chunk.FluidTicks.Unpack(GameTime);
        FluidTicks.AddContainer(chunk.Pos, chunk.FluidTicks);
    }

    //EnsureChunkTicksRegistered registers by coords; if the chunk cannot be fetched it retries next time
    public void EnsureChunkTicksRegistered(ChunkPos pos)
    {
        if (_tickRegisteredChunks.Contains(ChunkPos.Pack(pos.X, pos.Z))) return;
        var chunk = GetChunk(pos);
        if (chunk is not null) EnsureChunkTicksRegistered(chunk);
    }

    //UnregisterChunkTicks detaches the chunk's scheduled tick container, maps to vanilla LevelChunk.unregisterTickContainerFromLevel
    //Required on chunk unload; leaving the container in the set would double-register the chunk on reload and land new ticks on the old container
    public void UnregisterChunkTicks(ChunkPos pos)
    {
        if (!_tickRegisteredChunks.Remove(ChunkPos.Pack(pos.X, pos.Z))) return;
        _blockTicks?.RemoveContainer(pos);
        _fluidTicks?.RemoveContainer(pos);
    }

    //GetBlockState reads a block state; returns null when the chunk or section is not loaded
    public virtual BlockState? GetBlockState(BlockPos pos)
    {
        var chunk = GetChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        return chunk?.GetSection(pos.Y >> 4)?.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //GetLoadedChunk takes only a chunk already in memory without triggering a load
    //Levels without a chunk source fall back to plain GetChunk, having no notion of loading
    public virtual ChunkAccess? GetLoadedChunk(ChunkPos pos) => GetChunk(pos);

    //GetBlockStateIfLoaded reads a block state without triggering a load; a chunk not in memory gives null
    //Per-tick entity bounding box queries must go through it: when a position borders an unloaded chunk
    //plain GetBlockState would pull up a chunk with no ticket, which is reclaimed the same tick, so the chunk unloads and loads repeatedly
    //Every unload writes to disk and clears the chunk's block entities, so cross-tick intermediates like pistons lose their block entities
    public BlockState? GetBlockStateIfLoaded(BlockPos pos)
    {
        var chunk = GetLoadedChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        return chunk?.GetSection(pos.Y >> 4)?.GetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15);
    }

    //IsLoaded: the position is within build height and its chunk is in memory, maps to vanilla isLoaded
    //Vanilla judges by the chunk load level; this project only checks whether the chunk is in memory, enough for predicates and command queries
    public virtual bool IsLoaded(BlockPos pos)
        => pos.Y >= MinBuildHeight && pos.Y < MaxBuildHeight
            && GetLoadedChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4)) is not null;

    //GetFluidState: the fluid state at the position, giving empty fluid when the block state is unavailable, maps to vanilla getFluidState
    public virtual FluidState GetFluidState(BlockPos pos)
        => GetBlockState(pos)?.FluidState ?? FluidState.Empty;

    //GetMaxLocalRawBrightness: the position's max local brightness, the larger of block light and sky light, maps to vanilla getMaxLocalRawBrightness
    public virtual int GetMaxLocalRawBrightness(BlockPos pos)
        => Math.Max(
            GetLightValue(NetCraft.Registry.LightLayer.Block, pos),
            GetLightValue(NetCraft.Registry.LightLayer.Sky, pos));

    //CanSeeSky: the position sees the sky directly; sky light 15 counts as visible, maps to vanilla canSeeSky
    public virtual bool CanSeeSky(BlockPos pos)
        => GetLightValue(NetCraft.Registry.LightLayer.Sky, pos) >= 15;

    //GetBiome: the biome of the chunk at the position, null when the chunk is not in memory, maps to vanilla getBiome
    public virtual Holder<Biome>? GetBiome(BlockPos pos)
        => GetLoadedChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4))?
            .GetNoiseBiome(pos.X >> 2, pos.Y >> 2, pos.Z >> 2);

    //UpdateLight, light recomputation after a block change; a no-op for levels without a light engine
    public virtual void UpdateLight(BlockPos pos) { }

    //GetLightValue reads the given light layer's value at the position; levels without a light engine return 0
    //Blocks that output by brightness like daylight detectors need the real sky light; when the base cannot provide it, treat as fully dark
    public virtual int GetLightValue(NetCraft.Registry.LightLayer layer, BlockPos pos) => 0;

    //SetBlock writes a block and completes the interactions; the 3-arg version fills in the default propagation depth, maps to the vanilla same-named overload
    public bool SetBlock(BlockPos pos, BlockState state, int updateFlags = BlockUpdateFlags.All)
        => SetBlock(pos, state, updateFlags, BlockUpdateFlags.UpdateLimitDefault);

    //SetBlock writes a block and completes the interactions, maps to vanilla Level.setBlock
    //Order: write state -> light -> block entity removal -> removal effects -> onPlace -> neighbor updates -> shape updates
    //There is no "strict mode early return"; values like 816 only make the matching checks miss and the method still runs to the end
    public bool SetBlock(BlockPos pos, BlockState state, int updateFlags, int updateLimit)
    {
        var previous = WriteBlockState(pos, state);
        if (previous is null || previous.Value == state) return false;
        var oldState = previous.Value;
        var movedByPiston = (updateFlags & BlockUpdateFlags.MoveByPiston) != 0;
        //Only a change of block type counts as swapping a block; a state change on the same block does not
        var blockChanged = !ReferenceEquals(oldState.Owner, state.Owner);

        //A redstone component change is the first scene when diagnosing a signal chain; record the old and new states plus the side-effect flags
        if (RedstoneIds.IsRedstoneComponent(oldState.Owner.Id) || RedstoneIds.IsRedstoneComponent(state.Owner.Id))
            Log.Debug($"redstone write {pos} {oldState.Owner.Id}:{oldState.Id} -> {state.Owner.Id}:{state.Id} flags={updateFlags} changed={blockChanged}");

        //Mark the light dirty, maps to updateSectionStatus and checkBlock in vanilla LevelChunk.setBlockState
        //This cannot be filtered by hasDifferentLightProperties: when a section goes from fully empty to holding blocks like glass panes the light properties do not change
        //but the section empty flag must update too, or the light engine keeps treating it as empty
        //Marking only enqueues; the actual propagation and dispatch happen once at the end of each tick
        UpdateLight(pos);

        if (blockChanged && (updateFlags & BlockUpdateFlags.SkipBlockEntitySideEffects) == 0)
        {
            BlockUpdateSink?.RemoveBlockEntity(pos);
            BlockUpdateSink?.AddBlockEntity(pos, state);
        }

        if (blockChanged && ((updateFlags & BlockUpdateFlags.Neighbours) != 0 || movedByPiston)
            && oldState.Owner is IBlockUpdateBehaviour oldBehaviour)
            oldBehaviour.AffectNeighborsAfterRemoval(this, pos, oldState, movedByPiston);

        if ((updateFlags & BlockUpdateFlags.SkipOnPlace) == 0 && state.Owner is IBlockUpdateBehaviour newBehaviour)
            newBehaviour.OnPlace(this, pos, state, oldState, movedByPiston);

        if ((updateFlags & BlockUpdateFlags.Neighbours) != 0)
            UpdateNeighborsAt(pos, oldState.Owner);

        //Clear the neighbors and suppress-drops bits before the shape update, maps to vanilla updateFlags & -34
        if ((updateFlags & BlockUpdateFlags.KnownShape) == 0 && updateLimit > 0)
        {
            var shapeFlags = updateFlags & ~(BlockUpdateFlags.Neighbours | BlockUpdateFlags.SuppressDrops);
            var nextLimit = updateLimit - 1;
            BlockUpdateHelper.UpdateIndirectNeighbourShapes(this, oldState, pos, shapeFlags, nextLimit);
            BlockUpdateHelper.UpdateNeighbourShapes(this, state, pos, shapeFlags, nextLimit);
            BlockUpdateHelper.UpdateIndirectNeighbourShapes(this, state, pos, shapeFlags, nextLimit);
        }

        //Client sync comes after the side-effect chain, maps to the vanilla branch when flags contains UPDATE_CLIENTS
        if ((updateFlags & BlockUpdateFlags.Clients) != 0)
            BlockUpdateSink?.BlockChanged(pos, state);

        return true;
    }

    //LevelEvent broadcasts a world event, maps to vanilla Level.levelEvent
    public void LevelEvent(int kind, BlockPos pos, int data)
        => BlockUpdateSink?.LevelEvent(kind, pos, data);

    //GetBlockEntity gets the block entity at that pos; the block entity container is in the Game layer and provided by the side-effect sink
    public T? GetBlockEntity<T>(BlockPos pos) where T : class
        => BlockUpdateSink?.GetBlockEntity(pos) as T;

    //BlockEntityChanged syncs the client after block entity data changes
    public void BlockEntityChanged(BlockPos pos)
        => BlockUpdateSink?.BlockEntityChanged(pos);

    //SetBlockEntity puts in an already-built block entity, maps to vanilla Level.setBlockEntity
    //When a piston pushes, the block entity must carry the moved flag and motion params, so it cannot go through the create-by-state path
    public void SetBlockEntity(object entity)
        => BlockUpdateSink?.SetBlockEntity(entity);

    //RemoveBlockEntity removes the block entity at that pos, maps to vanilla Level.removeBlockEntity
    public void RemoveBlockEntity(BlockPos pos)
        => BlockUpdateSink?.RemoveBlockEntity(pos);

    //PlaySound plays a sound at the block pos, maps to vanilla Level.playSound without an entity
    public void PlaySound(SoundEvent sound, SoundSource source, BlockPos pos, float volume, float pitch)
        => BlockUpdateSink?.PlaySound(sound, source, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, volume, pitch);

    //WriteBlockState writes only the state and heightmap without any interactions, maps to the write part of vanilla LevelChunk.setBlockState
    protected virtual BlockState? WriteBlockState(BlockPos pos, BlockState state)
    {
        var chunk = GetChunk(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        var section = chunk?.GetSection(pos.Y >> 4);
        var previous = section?.SetBlockState(pos.X & 15, pos.Y & 15, pos.Z & 15, state);
        if (previous is not null && chunk is not null)
            chunk.UpdateHeightmaps(pos.X, pos.Y, pos.Z, state);
        return previous;
    }

    //UpdateNeighborsAt sends neighbor updates in all six directions, maps to vanilla updateNeighborsAt
    public void UpdateNeighborsAt(BlockPos pos, NetCraft.Registry.Block sourceBlock)
        => NeighborUpdater.UpdateNeighborsAtExceptFromFacing(pos, sourceBlock, null);

    //UpdateNeighborsAtExceptFromFacing skips neighbor updates in the given direction, maps to the vanilla same-named method
    public void UpdateNeighborsAtExceptFromFacing(BlockPos pos, NetCraft.Registry.Block sourceBlock,
        Direction? skipDirection)
        => NeighborUpdater.UpdateNeighborsAtExceptFromFacing(pos, sourceBlock, skipDirection);

    //NeighborChanged enqueues a single-point neighbor update; the state is re-read on execution
    public void NeighborChanged(BlockPos pos, NetCraft.Registry.Block changedBlock)
        => NeighborUpdater.NeighborChanged(pos, changedBlock);

    //NeighborChanged with a state snapshot; not re-read on execution
    public void NeighborChanged(BlockPos pos, BlockState state, NetCraft.Registry.Block changedBlock,
        bool movedByPiston)
        => NeighborUpdater.NeighborChanged(pos, state, changedBlock, movedByPiston);

    //NeighborShapeChanged enqueues a shape update, maps to vanilla neighborShapeChanged
    //pos is the block being updated; neighbourPos is the source block that triggered this update
    public void NeighborShapeChanged(Direction direction, BlockPos pos, BlockPos neighbourPos,
        BlockState neighbourState, int updateFlags, int updateLimit)
        => NeighborUpdater.ShapeUpdate(direction, neighbourState, pos, neighbourPos, updateFlags, updateLimit);

    //ScheduleTick schedules a block tick, maps to vanilla LevelAccessor.scheduleTick
    public void ScheduleTick(BlockPos pos, NetCraft.Registry.Block block, int delay)
        => ScheduleTick(pos, block, delay, TickPriority.Normal);

    public void ScheduleTick(BlockPos pos, NetCraft.Registry.Block block, int delay, TickPriority priority)
    {
        //The target chunk may not have a container registered yet; register it in place or this tick is dropped
        EnsureChunkTicksRegistered(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        BlockTicks.Schedule(new ScheduledTick<NetCraft.Registry.Block>(
            block, pos, GameTime + delay, priority, _subTickCount++));
        //Whether the scheduled tick got queued is the most important thing to confirm when redstone does not act
        if (RedstoneIds.IsRedstoneComponent(block.Id))
            Log.Debug($"redstone schedule {pos} block={block.Id} delay={delay} priority={priority} now={GameTime} due={GameTime + delay}");
    }

    //ScheduleTick schedules a fluid tick, maps to the fluid overload of vanilla LevelAccessor.scheduleTick
    public void ScheduleTick(BlockPos pos, NetCraft.Registry.Fluid fluid, int delay)
        => ScheduleTick(pos, fluid, delay, TickPriority.Normal);

    public void ScheduleTick(BlockPos pos, NetCraft.Registry.Fluid fluid, int delay, TickPriority priority)
    {
        EnsureChunkTicksRegistered(new ChunkPos(pos.X >> 4, pos.Z >> 4));
        FluidTicks.Schedule(new ScheduledTick<NetCraft.Registry.Fluid>(
            fluid, pos, GameTime + delay, priority, _subTickCount++));
    }

    //HasScheduledTick, whether the same block's tick is already scheduled at that pos
    public bool HasScheduledTick(BlockPos pos, NetCraft.Registry.Block block)
        => BlockTicks.HasScheduledTick(pos, block);

    //WillTickThisTick, whether the block tick at that pos has been collected this tick
    public bool WillTickThisTick(BlockPos pos, NetCraft.Registry.Block block)
        => BlockTicks.WillTickThisTick(pos, block);

    //TickBlockTicks advances block scheduled ticks; the budget matches the 65536 in vanilla ServerLevel.tick
    public void TickBlockTicks(int maxTicksToProcess = 65536)
        => BlockTicks.Tick(GameTime, maxTicksToProcess, TickBlock);

    //TickFluidTicks advances fluid scheduled ticks with the same budget as block ticks, maps to FluidTicks.tick in vanilla ServerLevel.tick
    public void TickFluidTicks(int maxTicksToProcess = 65536)
        => FluidTicks.Tick(GameTime, maxTicksToProcess, TickFluid);

    //TickFluid, the fluid tick callback; dropped when the fluid now at the pos is not the same kind registered at scheduling
    private void TickFluid(BlockPos pos, NetCraft.Registry.Fluid fluid)
    {
        var state = GetBlockState(pos);
        if (state is null) return;
        var fluidState = GetFluidState(pos);
        if (!ReferenceEquals(fluidState.Type, fluid)) return;
        if (fluid is IFluidBehaviour behaviour)
            behaviour.Tick(this, pos, state.Value, fluidState);
    }

    //TickBlock runs one block tick, maps to vanilla ServerLevel.tickBlock
    //The block at the pos must still be the one scheduled, otherwise it is dropped; vanilla relies on this to filter stale ticks
    private void TickBlock(BlockPos pos, NetCraft.Registry.Block block)
    {
        var state = GetBlockState(pos);
        //Log stale ticks too, otherwise you only see "a tick was scheduled but nothing happened"
        if (state is null || !ReferenceEquals(state.Value.Owner, block))
        {
            if (RedstoneIds.IsRedstoneComponent(block.Id))
                Log.Debug($"redstone dropped expired tick {pos} block={block.Id} owner={state?.Owner.Id.ToString() ?? "chunk not loaded"}");
        }
        if (RedstoneIds.IsRedstoneComponent(block.Id))
            Log.Debug($"redstone run {pos} block={block.Id} state={state.Value.Id} time={GameTime}");
        if (state.Value.Owner is IBlockUpdateBehaviour behaviour)
            behaviour.Tick(this, pos, state.Value, Random);
    }

    //BlockEvent enqueues a block event, maps to vanilla ServerLevel.blockEvent
    public void BlockEvent(BlockPos pos, NetCraft.Registry.Block block, int paramA, int paramB)
    {
        var data = new BlockEventData(pos, block, paramA, paramB);
        if (_blockEventSet.Add(data)) _blockEvents.AddLast(data);
    }

    //FlushBlockUpdates flushes this tick's accumulated block changes, maps to broadcastChanges in vanilla chunkSource.tick
    public void FlushBlockUpdates() => BlockUpdateSink?.FlushBlockUpdates();

    //RunBlockEvents runs the block event queue, maps to vanilla ServerLevel.runBlockEvents
    //Events outside the tickable range this tick defer to a later tick; a block type mismatch is dropped outright
    //Once a block accepts the event it is broadcast to clients through the side-effect sink with the vanilla three parameters
    public void RunBlockEvents()
    {
        _blockEventsToReschedule.Clear();
        while (_blockEvents.First is { } node)
        {
            var data = node.Value;
            _blockEvents.RemoveFirst();
            _blockEventSet.Remove(data);
            if (!IsPositionTicking(ChunkPos.Pack(data.Pos.X >> 4, data.Pos.Z >> 4)))
            {
                _blockEventsToReschedule.Add(data);
                continue;
            }
            var state = GetBlockState(data.Pos);
            if (state is null || !ReferenceEquals(state.Value.Owner, data.Block)) continue;
            if (state.Value.Owner is IBlockUpdateBehaviour behaviour
                && behaviour.TriggerEvent(this, data.Pos, state.Value, data.ParamA, data.ParamB))
                BlockUpdateSink?.BlockEvent(data.Pos, data.Block, data.ParamA, data.ParamB);
        }

        foreach (var data in _blockEventsToReschedule)
            if (_blockEventSet.Add(data)) _blockEvents.AddLast(data);
    }

    //GetSignal reads the signal the given pos outputs in a direction, maps to vanilla SignalGetter.getSignal
    //A conducting block merges the six directions' direct signals into its own; the signal strength and direct signal take the max
    //direction points from the receiver toward the queried block, as in vanilla
    public int GetSignal(BlockPos pos, Direction direction)
    {
        var state = GetBlockState(pos);
        if (state is null || state.Value.Owner is not IBlockSignalBehaviour behaviour) return 0;
        var current = state.Value;
        var signal = behaviour.GetSignal(this, pos, current, direction);
        if (behaviour.IsRedstoneConductor(this, pos, current))
            return Math.Max(signal, GetDirectSignalTo(pos));
        return signal;
    }

    //GetDirectSignal reads the direct signal at the given pos, maps to vanilla SignalGetter.getDirectSignal
    public int GetDirectSignal(BlockPos pos, Direction direction)
    {
        var state = GetBlockState(pos);
        if (state is null || state.Value.Owner is not IBlockSignalBehaviour behaviour) return 0;
        return behaviour.GetDirectSignal(this, pos, state.Value, direction);
    }

    //GetDirectSignalTo takes the max of the six directions' direct signals, returning early at 15, maps to vanilla getDirectSignalTo
    //The unrolled order DOWN UP NORTH SOUTH WEST EAST matches vanilla and must not become an iteration over Values
    public int GetDirectSignalTo(BlockPos pos)
    {
        var signal = Math.Max(0, GetDirectSignal(pos.Offset(Direction.Down), Direction.Down));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.Up), Direction.Up));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.North), Direction.North));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.South), Direction.South));
        if (signal >= 15) return signal;
        signal = Math.Max(signal, GetDirectSignal(pos.Offset(Direction.West), Direction.West));
        if (signal >= 15) return signal;
        return Math.Max(signal, GetDirectSignal(pos.Offset(Direction.East), Direction.East));
    }

    //HasSignal, whether the given pos has a signal in a direction, maps to vanilla SignalGetter.hasSignal
    public bool HasSignal(BlockPos pos, Direction direction) => GetSignal(pos, direction) > 0;

    //HasNeighborSignal, whether any of the six directions has a signal, maps to vanilla hasNeighborSignal
    public bool HasNeighborSignal(BlockPos pos)
    {
        foreach (var direction in Direction.Values)
            if (GetSignal(pos.Offset(direction), direction) > 0) return true;
        return false;
    }

    //GetBestNeighborSignal takes the max of the six directions' signals, returning early at 15, maps to vanilla getBestNeighborSignal
    public int GetBestNeighborSignal(BlockPos pos)
    {
        var best = 0;
        foreach (var direction in Direction.Values)
        {
            var signal = GetSignal(pos.Offset(direction), direction);
            if (signal >= 15) return 15;
            if (signal > best) best = signal;
        }
        return best;
    }

    //GetBestOwnOrNeighbourSignal takes the max of its own and neighbor signals, maps to vanilla getBestOwnOrNeighbourSignal
    public int GetBestOwnOrNeighbourSignal(BlockPos pos)
    {
        var state = GetBlockState(pos);
        var own = state is not null && state.Value.Owner is IBlockSignalBehaviour { IsSignalSource: true } behaviour
            ? behaviour.OwnSignal(this, pos, state.Value)
            : 0;
        return Math.Max(GetBestNeighborSignal(pos), own);
    }

    //GetControlInputSignal reads the control input signal, maps to vanilla SignalGetter.getControlInputSignal
    //Repeater side locking and comparator side input both go through it
    //When onlyDiodes is true only diodes count; vanilla repeater locking uses this to exclude redstone wire
    public int GetControlInputSignal(BlockPos pos, Direction direction, bool onlyDiodes)
    {
        var state = GetBlockState(pos);
        if (state is null || state.Value.Owner is not IBlockSignalBehaviour behaviour) return 0;
        var current = state.Value;
        if (onlyDiodes) return behaviour.IsDiode ? GetDirectSignal(pos, direction) : 0;
        //Redstone block is always 15, redstone wire reads its own power, other signal sources read the direct signal, matching the vanilla three branches
        if (current.Owner.Id == RedstoneIds.Block) return 15;
        if (current.Owner.Id == RedstoneIds.Wire)
            return current.HasProperty(BlockStateProperties.Power)
                ? current.GetValue(BlockStateProperties.Power)
                : 0;
        return behaviour.IsSignalSource ? GetDirectSignal(pos, direction) : 0;
    }

    //ExtraEntityBoxes, bounding boxes from outside the entity manager; players are not in the entity manager and are injected by the Game layer
    public Func<IEnumerable<AABB>>? ExtraEntityBoxes { get; set; }

    //EntityBoxes, all bounding boxes taking part in the entity-inside test; subclasses wire in the entity manager's entities
    protected virtual IEnumerable<AABB> EntityBoxes()
        => ExtraEntityBoxes?.Invoke() ?? Enumerable.Empty<AABB>();

    //DispatchEntityInside dispatches the entity-inside-block callback, maps to vanilla Entity.checkInsideBlocks
    //Called after each entity move per tick, dispatching to each block covered by a slightly shrunk bounding box
    //Block reads use the non-loading variant: this runs every tick and must not incidentally pull up an unloaded neighboring chunk
    public void DispatchEntityInside()
    {
        foreach (var box in EntityBoxes())
        {
            var shrunk = box.Deflate(1.0E-3);
            var minX = Mth.Floor(shrunk.Min.X);
            var maxX = Mth.Floor(shrunk.Max.X);
            var minY = Mth.Floor(shrunk.Min.Y);
            var maxY = Mth.Floor(shrunk.Max.Y);
            var minZ = Mth.Floor(shrunk.Min.Z);
            var maxZ = Mth.Floor(shrunk.Max.Z);
            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                    for (var z = minZ; z <= maxZ; z++)
                    {
                        var pos = new BlockPos(x, y, z);
                        var state = GetBlockStateIfLoaded(pos);
                        if (state?.Owner is IEntityInsideBehaviour behaviour)
                            behaviour.OnEntityInside(this, pos, state.Value);
                    }
        }
    }

    //CountEntitiesInBox counts entities in the box, used by pressure plates for signal strength, maps to the counting use of vanilla getEntitiesOfClass
    //Players are counted separately through ExtraEntityBoxes; level entities are left to subclasses
    public int CountEntitiesInBox(AABB box)
    {
        var count = 0;
        if (ExtraEntityBoxes is not null)
            foreach (var playerBox in ExtraEntityBoxes())
                if (box.Intersects(playerBox)) count++;
        return count + CountLevelEntitiesInBox(box);
    }

    //CountLevelEntitiesInBox counts entities in the level entity manager; 0 for levels without one
    protected virtual int CountLevelEntitiesInBox(AABB box) => 0;

    //EntitiesInBox, level entities in the box, shared by placement placeholder checks and future entity collision
    //Players are not in the entity manager; the caller counts ExtraEntityBoxes separately when needed
    public IEnumerable<NetCraft.Registry.Entity> EntitiesInBox(AABB box) => LevelEntitiesInBox(box);

    //LevelEntitiesInBox, entities intersecting the box in the level entity manager; empty for levels without one
    protected virtual IEnumerable<NetCraft.Registry.Entity> LevelEntitiesInBox(AABB box)
        => Enumerable.Empty<NetCraft.Registry.Entity>();
}
