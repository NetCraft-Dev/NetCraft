using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;

namespace NetCraft.Game.Server;

//ServerBlockUpdates server-side block change orchestration, maps to the caller of vanilla Level.setBlock
//The full interaction chain has moved down to ServerLevel.SetBlock; this only handles client broadcast and player-side presentation
//The broadcast range is currently all online players, narrowed to view distance once tracking is wired up
public static class ServerBlockUpdates
{
    //CenterOfBlock the default when no hit point is available, computed from the block center
    private static readonly Vec3 CenterOfBlock = new(0.5, 0.5, 0.5);

    //SetBlock writes a block and broadcasts by flags, returning whether a change actually happened
    //strict maps to vanilla UPDATE_SKIP_ALL_SIDEEFFECTS: only the state lands, and the client sees it after a chunk reload
    //When notifyNeighbors is false no neighbor update is triggered but the client is still synced, maps to vanilla's UPDATE_CLIENTS only
    //The broadcast itself is sent uniformly by the level side-effect exit, see ServerBlockUpdateSink.BlockChanged
    public static bool SetBlock(PersistentServerLevel level, PlayerList players, BlockPos pos,
        BlockState state, bool notifyNeighbors = true, bool strict = false)
    {
        var flags = strict
            ? BlockUpdateFlags.SkipAllSideEffects
            : notifyNeighbors
                ? BlockUpdateFlags.All
                : BlockUpdateFlags.Clients;
        return level.SetBlock(pos, state, flags);
    }

    //ParticleBlockBreak the world event id for breaking a block, maps to vanilla LevelEvent.PARTICLE_BLOCK_BREAK
    //The client plays the break particles and the block's own break sound from the block state id in the packet; no sound_event registry needed
    public const int ParticleBlockBreak = 2001;

    //BreakBlock a player breaking a block: break effect -> drops -> destroy callback -> set air
    //player may be null for non-player destruction, in which case PlayerDestroy is not triggered
    public static bool BreakBlock(PersistentServerLevel level, PlayerList players, ServerPlayer? player, BlockPos pos)
    {
        var state = level.GetBlockState(pos);
        //An empty position is not broken, also avoiding triggering behavior on air
        if (state is null || state.Value.Owner.IsAir) return false;

        //The break effect is sent before the block changes, carrying the broken block's state id for the client particles and dig sound
        //Without it the block vanishes out of nowhere for others and digging has no sound, maps to the levelEvent of vanilla Level.destroyBlock
        //The breaker is excluded since his client already played it locally while digging; receiving another would double it
        var breakEvent = new ClientboundLevelEventPacket(ParticleBlockBreak, pos, state.Value.Id, false);
        if (player is null) players.BroadcastAll(breakEvent);
        else players.BroadcastAllExcept(player, breakEvent);

        //Creative breaking does not drop, maps to the canDrop branch of vanilla ServerPlayerGameMode.removeBlock
        var dropItems = player is null || player.GameType != NetCraft.Game.World.Level.GameType.Creative;
        var behaviour = state.Value.Owner as BlockBehaviour;
        if (behaviour is not null)
        {
            //Vanilla order puts playerWillDestroy before removal and drops; multi-block shapes remove the other half without drops here
            if (player is not null) behaviour.PlayerWillDestroy(level, player, pos, state.Value);
            if (dropItems)
            {
                foreach (var drop in behaviour.GetDrops(level, player, pos, state.Value))
                    SpawnDrop(level, pos, drop);
            }
            if (player is not null) behaviour.PlayerDestroy(level, player, pos, state.Value);
        }
        var changed = SetBlock(level, players, pos, Blocks.AIR.DefaultBlockState);
        //destroy is ordered after the set-air; called only when it was actually replaced
        //A moving piston uses it to take the cell in the opposite FACING; the base does not get stuck extended when a player breaks a block mid-move
        if (changed && behaviour is not null) behaviour.Destroy(level, pos, state.Value);
        return changed;
    }

    //DestroyBlock destroys a block for non-player reasons, maps to the drops and set-air parts of vanilla Level.destroyBlock
    //Called by the update chain's side-effect exit when the shape update computes air
    public static bool DestroyBlock(PersistentServerLevel level, PlayerList players, BlockPos pos, bool dropItems)
    {
        var state = level.GetBlockState(pos);
        if (state is null || state.Value.Owner.IsAir) return false;
        if (dropItems && state.Value.Owner is BlockBehaviour behaviour)
        {
            foreach (var drop in behaviour.GetDrops(level, null, pos, state.Value))
                SpawnDrop(level, pos, drop);
        }
        return SetBlock(level, players, pos, Blocks.AIR.DefaultBlockState);
    }

    //SpawnDrop spawns the drop entity after a block break, maps to vanilla Block.popResource
    //The landing point is the block center with ±0.25 jitter raised to the block half-height minus the item half-height; the initial velocity is given by the entity constructor
    //Pickup delay 10 ticks, so it is not sucked back into the inventory right after mining
    //Campfire-cooked outputs also go through it; the drop presentation matches block breaking
    public static void SpawnDrop(PersistentServerLevel level, BlockPos pos, ItemStack stack)
    {
        if (stack.IsEmpty()) return;
        var halfHeight = EntityTypes.ITEM.Height / 2.0;
        var drop = new ItemEntity(EntityTypes.ITEM,
            pos.X + 0.5 + Random.Shared.NextDouble() * 0.5 - 0.25,
            pos.Y + 0.5 + Random.Shared.NextDouble() * 0.5 - 0.25 - halfHeight,
            pos.Z + 0.5 + Random.Shared.NextDouble() * 0.5 - 0.25,
            stack);
        drop.SetDefaultPickUpDelay();
        level.AddEntity(drop);
    }

    //UseOn a player using on a block, handed to the block's own behavior; returns whether it was handled
    public static bool UseOn(PersistentServerLevel level, ServerPlayer player, BlockPos pos, Direction face)
    {
        var state = level.GetBlockState(pos);
        return state is not null
            && state.Value.Owner is BlockBehaviour behaviour
            && behaviour.UseOn(level, player, pos, state.Value, face);
    }

    //UseItemOn a player using a held item on a block, maps to vanilla ItemStack.useOn
    //Vanilla order puts the block behavior first and the item second, reaching the item only when the block does not handle it; flint and steel ignition goes here
    public static bool UseItemOn(PersistentServerLevel level, ServerPlayer player, BlockPos pos, Direction face,
        InteractionHand hand)
    {
        var held = hand == InteractionHand.OffHand
            ? player.Inventory.GetItem(PlayerInventory.OffhandSlot)
            : player.Inventory.GetSelectedItem();
        return !held.IsEmpty() && held.GetItem() is IUseOnBlockItem item
            && item.UseOn(level, player, held, pos, face);
    }

    //PlaceHeldBlock a player using a held block item on a block face places a block
    //maps to the vanilla ItemStack.useOn -> BlockItem.place chain; lands as the default state of the item's block
    //The held item is the interaction hand reported by the client; a block in the offhand should also place when the main hand is empty
    //The target position must be replaceable; adventure/spectator refuse to place; a successful non-creative placement consumes one item
    public static bool PlaceHeldBlock(PersistentServerLevel level, PlayerList players, ServerPlayer player,
        BlockPos pos, Direction face, InteractionHand hand, Vec3? hitLocal = null)
    {
        var gameType = player.GameType;
        if (gameType.IsBlockPlacingRestricted) return false;

        var held = hand == InteractionHand.OffHand
            ? player.Inventory.GetItem(PlayerInventory.OffhandSlot)
            : player.Inventory.GetSelectedItem();
        if (held.IsEmpty() || held.GetItem() is not BlockItem blockItem) return false;

        var placePos = pos.Offset(face);
        var target = level.GetBlockState(placePos);
        if (target?.Owner is not BlockBehaviour targetBehaviour || !targetBehaviour.CanBeReplaced)
            return false;

        //Clicking a horizontal side lands the wall variant, maps to vanilla StandingAndWallBlockItem.getStateForPlacement
        var placedBlock = face.IsHorizontal && blockItem.WallBlock is { } wall
            ? wall
            : blockItem.PlacedBlock;
        //The facing and attachment face are computed by the block from the context; levers and torches use it to land the right state
        //Observers need six directions so an extra player look direction is given, maps to vanilla getNearestLookingDirection
        var placedState = placedBlock is BlockBehaviour placedBehaviour
            ? placedBehaviour.GetStateForPlacement(level, placePos, face, Direction.FromYRot(player.Yaw),
                player.GetNearestLookingDirection(), hitLocal ?? CenterOfBlock)
            : placedBlock.DefaultBlockState;
        if (placedState is not { } state) return false;
        //A placement that cannot stand is refused, maps to the canSurvive check in vanilla BlockItem.place
        var placementBehaviour = state.Owner as BlockBehaviour;
        if (placementBehaviour is not null && !placementBehaviour.CanSurvive(level, placePos, state))
            return false;

        //Shape obstruction check: placement fails when the new block crushes an entity, maps to isUnobstructed in vanilla BlockItem.canPlace
        //The player also counts as obstructing (vanilla passes null here without excluding the placer); the clicked position usually does not intersect the placer
        CollisionGetter collisionView = new LevelCollisionGetter(level, level.MinSectionY, level.SectionsCount);
        if (!collisionView.IsUnobstructed(state, placePos, CollisionContext.PlacementContext(null)))
            return false;

        if (!SetBlock(level, players, placePos, state)) return false;
        //After placement, flush the item's block entity data into the new block entity, maps to vanilla updateCustomBlockEntityTag
        BlockItem.UpdateCustomBlockEntityTag(level, placePos, held);
        //A post-placement callback; repeaters use it to schedule their first tick, maps to vanilla Block.setPlacedBy
        placementBehaviour?.SetPlacedBy(level, placePos, state, player);

        //Vanilla's consume check is abilities.instabuild; creative does not consume
        if (gameType != NetCraft.Game.World.Level.GameType.Creative)
        {
            held.SetCount(held.GetCount() - 1);
            player.ContainerMenu?.SendAllDataToRemote();
        }
        return true;
    }

    //NotifyNeighbors sends neighbor updates in six directions from the position, maps to the neighbor part of vanilla updateNeighboursOnBlockSet
    //Used by batch changes with a unified flush; single-cell changes are already done inside ServerLevel.SetBlock
    internal static void NotifyNeighbors(PersistentServerLevel level, BlockPos pos, BlockState state)
        => level.UpdateNeighborsAt(pos, state.Owner);
}

//BlockChangeBatch batch block changes, maps to vanilla fill's flow of suppressing intermediate side effects with updateFlags then flushing uniformly
//Vanilla calls setBlock(flags=2|256) per cell, writing only the state and marking the section changed, with neighbor updates turned off by flags
//The block packet is merged per section into section_blocks_update at the end of the tick by ServerChunkCache
//With per-cell SetBlock each cell sends a block_update and runs a full light propagation round; filling a large area is O(cells) full propagation rounds
public sealed class BlockChangeBatch
{
    private readonly PersistentServerLevel _level;
    //_states changed position to final state; a position written multiple times keeps only the last
    private readonly Dictionary<BlockPos, BlockState> _states = new();

    public BlockChangeBatch(PersistentServerLevel level) => _level = level;

    //Apply writes one cell and returns whether a change actually happened; sends no packets and notifies no neighbors
    //flags keep only skip-block-entity-side-effects; client sync is merged per section by Flush, and including it here would degrade to per-cell packets
    public bool Apply(BlockPos pos, BlockState state)
    {
        if (!_level.SetBlock(pos, state, BlockUpdateFlags.SkipBlockEntitySideEffects))
            return false;
        _states[pos] = state;
        return true;
    }

    //Flush flushes light, block packets and neighbor notifications uniformly, maps to vanilla's command-layer updateNeighboursOnBlockSet
    //sideEffects false maps to vanilla UPDATE_SKIP_ALL_SIDEEFFECTS: only the state lands, visible after a chunk reload
    public void Flush(PlayerList players, bool sideEffects = true)
    {
        if (_states.Count == 0) return;
        if (!sideEffects)
        {
            _states.Clear();
            return;
        }
        var positions = new List<BlockPos>(_states.Keys);
        _level.UpdateLightBatch(positions);
        BroadcastBySection(players);
        foreach (var (pos, state) in _states) ServerBlockUpdates.NotifyNeighbors(_level, pos, state);
        _states.Clear();
    }

    //BroadcastBySection merges by section into section_blocks_update, maps to vanilla ChunkHolder.broadcastChanges
    //Changes in one section are packed into one packet; filling a large area drops the packet count from O(cells) to O(sections)
    private void BroadcastBySection(PlayerList players)
    {
        var sections = new Dictionary<SectionPos, List<long>>();
        foreach (var (pos, state) in _states)
        {
            var section = SectionPos.Of(pos);
            if (!sections.TryGetValue(section, out var packed))
                sections[section] = packed = new List<long>();
            //Each entry is (state id << 12) | the 12-bit offset within the section, consistent with vanilla ClientboundSectionBlocksUpdatePacket
            packed.Add(((long)state.Id << 12) | (ushort)SectionPos.SectionRelativePos(pos));
        }
        foreach (var (section, packed) in sections)
            players.BroadcastAll(new ClientboundSectionBlocksUpdatePacket(section, packed.ToArray()));
    }
}
