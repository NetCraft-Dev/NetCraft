using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block.Piston;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
//Direction exists in both Primitives and Registry.Enums; the one used for blocks is taken here
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//P-1 piston family blocks, maps to the vanilla net.minecraft.world.level.block.piston package
//The base handles signals and moving, the moving piston takes the place of a pushed block and the piston head is the extended segment
//Push structure resolution is in Piston.PistonStructureResolver, animation and squeezing are in Piston.PistonMovingBlockEntity
public static partial class Blocks
{
    public static readonly PistonBaseBlock PISTON = new("piston", false);
    public static readonly PistonBaseBlock STICKY_PISTON = new("sticky_piston", true);
    public static readonly MovingPistonBlock MOVING_PISTON = new("moving_piston");

    //RegisterPiston registers the piston family into the real block table
    private static void RegisterPiston(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { PISTON, STICKY_PISTON, MOVING_PISTON };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //PistonBaseBlock piston base, maps to vanilla PistonBaseBlock
    //Extends when powered and retracts when unpowered over two ticks; once extended only the base remains here and the arm is carried by piston_head
    public sealed class PistonBaseBlock : NamedBlock
    {
        //TriggerExtend extend event, maps to vanilla TRIGGER_EXTEND
        public const int TriggerExtend = 0;

        //TriggerContract contract event, maps to vanilla TRIGGER_CONTRACT
        public const int TriggerContract = 1;

        //TriggerDrop drops directly on retract without moving, maps to vanilla TRIGGER_DROP
        public const int TriggerDrop = 2;

        //Once extended the base is only a four-pixel-thick strip, the rest is left to the piston head
        private static readonly Dictionary<Direction, VoxelShape> ShapesExtended =
            Shapes.RotateAll(NetCraft.Registry.Block.BoxZ(16.0, 4.0, 16.0));

        private readonly bool _isSticky;

        public PistonBaseBlock(string name, bool isSticky) : base(name) => _isSticky = isSticky;

        //IsSticky a sticky piston pulls the block in front back with it on retract
        public bool IsSticky => _isSticky;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => state.GetValue(BlockStateProperties.Extended)
                ? ShapesExtended[state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive()]
                : Shapes.Block();

        //IsRedstoneConductor a piston is not a redstone conductor, maps to isRedstoneConductor(Blocks::never) in vanilla pistonProperties
        //Without turning it off the full-block collision shape of a retracted piston would count as a conductor
        //The conductor branch of getSignal would pull in the direct signals from all sides toward the piston
        //A button on the push face gives a direct signal downward and the piston would extend and shove off the very button pushing it, which vanilla avoids
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        //SetPlacedBy decides immediately whether to extend from the surrounding signals on placement, maps to vanilla setPlacedBy
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
            => CheckIfExtend(level, pos, state);

        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston) => CheckIfExtend(level, pos, state);

        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            //A state change on the same block is not a placement and a moving base is not re-evaluated either
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            if (level.GetBlockEntity<BlockEntity>(pos) is not null) return;
            CheckIfExtend(level, pos, state);
        }

        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState
                .SetValue(BlockStateProperties.FacingProperty, lookingDirection.Opposite.ToState())
                .SetValue(BlockStateProperties.Extended, false);

        //CheckIfExtend schedules a block event when the signal changes, maps to vanilla checkIfExtend
        //The actual moving happens in the event, delaying one tick like vanilla so multiple signal changes in the same tick merge
        private void CheckIfExtend(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive();
            var extend = HasNeighborSignal(level, pos, direction);
            if (extend && !state.GetValue(BlockStateProperties.Extended))
            {
                if (new PistonStructureResolver(level, pos, direction, true).Resolve())
                    level.BlockEvent(pos, state.Owner, TriggerExtend, direction.Id3D);
            }
            else if (!extend && state.GetValue(BlockStateProperties.Extended))
            {
                //Retracting while the previous segment is still moving downgrades to a drop, so unfinished pushes are not left behind
                var pushedPos = pos.Relative(direction, 2);
                var pushedState = level.GetBlockState(pushedPos);
                var eventId = TriggerContract;
                if (pushedState is { } pushed
                    && pushed.Owner.Id.Path == "moving_piston"
                    && pushed.GetValue(BlockStateProperties.FacingProperty) == direction.ToState()
                    && level.GetBlockEntity<PistonMovingBlockEntity>(pushedPos) is { } moving
                    && moving.IsExtending
                    && (moving.GetProgress(0f) < 0.5f || level.GameTime == moving.LastTicked
                        || level.IsHandlingTick))
                    eventId = TriggerDrop;
                level.BlockEvent(pos, state.Owner, eventId, direction.Id3D);
            }
        }

        //HasNeighborSignal signals from six directions plus the block above itself; the push face itself does not trigger, maps to vanilla getNeighborSignal
        private static bool HasNeighborSignal(ServerLevel level, BlockPos pos, Direction pushDirection)
        {
            foreach (var direction in Direction.Values)
            {
                if (direction == pushDirection) continue;
                if (level.GetSignal(pos.Offset(direction), direction) > 0) return true;
            }
            //This cell checks downward along its facing and also the cell above and its six neighbors except below, which is vanilla's quasi-connectivity
            //Quasi-connectivity lets a quasi-connectivity setup activate the piston through a block, regardless of whether it is a redstone conductor
            if (level.GetSignal(pos, Direction.Down) > 0) return true;
            var above = pos.Relative(Direction.Up, 1);
            foreach (var direction in Direction.Values)
            {
                if (direction == Direction.Down) continue;
                if (level.GetSignal(above.Offset(direction), direction) > 0) return true;
            }
            return false;
        }

        public override bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB)
        {
            var direction = state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive();
            var extendedState = state.SetValue(BlockStateProperties.Extended, true);
            var extend = HasNeighborSignal(level, pos, direction);
            //The signal changed again this tick, so the event is voided and rescheduled like vanilla
            if (extend && paramA is TriggerContract or TriggerDrop)
            {
                level.SetBlock(pos, extendedState, 2);
                return false;
            }
            if (!extend && paramA == TriggerExtend) return false;
            var random = level.Random;
            if (paramA == TriggerExtend)
            {
                if (!MoveBlocks(level, pos, direction, true)) return false;
                level.SetBlock(pos, extendedState, 67);
                level.PlaySound(SoundEvents.PistonExtend, SoundSource.Blocks, pos, 0.5f,
                    random.NextFloat() * 0.25f + 0.6f);
                return true;
            }
            if (paramA is not (TriggerContract or TriggerDrop)) return true;
            //Before retracting, the unfinished animation of the segment in front is settled first
            if (level.GetBlockEntity<PistonMovingBlockEntity>(pos.Offset(direction)) is { } previous)
                previous.FinalTick();
            var movingState = MOVING_PISTON.DefaultBlockState
                .SetValue(BlockStateProperties.FacingProperty, direction.ToState())
                .SetValue(BlockStateProperties.PistonTypeProperty,
                    _isSticky ? PistonType.sticky : PistonType.normal);
            level.SetBlock(pos, movingState, 276);
            level.SetBlockEntity(new PistonMovingBlockEntity(pos, movingState,
                DefaultBlockState.SetValue(BlockStateProperties.FacingProperty, Direction.ById(paramB & 7).ToState()),
                direction, false, true));
            level.UpdateNeighborsAt(pos, movingState.Owner);
            BlockUpdateHelper.UpdateNeighbourShapes(level, movingState, pos, 2,
                BlockUpdateFlags.UpdateLimitDefault);
            if (_isSticky)
            {
                var twoPos = pos.Offset(direction.StepX * 2, direction.StepY * 2, direction.StepZ * 2);
                var movingAhead = level.GetBlockState(twoPos);
                var pistonPiece = false;
                if (movingAhead is { } ahead
                    && ahead.Owner.Id.Path == "moving_piston"
                    && level.GetBlockEntity<PistonMovingBlockEntity>(twoPos) is { } aheadEntity
                    && aheadEntity.MoveDirection == direction && aheadEntity.IsExtending)
                {
                    aheadEntity.FinalTick();
                    pistonPiece = true;
                }
                if (!pistonPiece)
                {
                    //Pulls the cell ahead back if it can, otherwise deletes it
                    if (paramA == TriggerContract && movingAhead is { } aheadState && !aheadState.Owner.IsAir
                        && IsPushable(aheadState, level, twoPos, direction.Opposite, false, direction)
                        && (PistonPushReactions.Of(aheadState) == PushReaction.normal
                            || aheadState.Owner.Id.Path is "piston" or "sticky_piston"))
                        MoveBlocks(level, pos, direction, false);
                    else
                        level.SetBlock(pos.Offset(direction), Blocks.AIR.DefaultBlockState, 3);
                }
            }
            else
            {
                level.SetBlock(pos.Offset(direction), Blocks.AIR.DefaultBlockState, 3);
            }
            level.PlaySound(SoundEvents.PistonContract, SoundSource.Blocks, pos, 0.5f,
                random.NextFloat() * 0.15f + 0.6f);
            return true;
        }

        //IsPushable whether the block can be moved by a piston, maps to the vanilla static method
        //When allowDestroyable is true, blocks that are "destroyed before pushing" are allowed through
        public static bool IsPushable(BlockState state, ServerLevel level, BlockPos pos, Direction direction,
            bool allowDestroyable, Direction connectionDirection)
        {
            if (pos.Y < level.MinBuildHeight || pos.Y > level.MaxBuildHeight - 1
                || !level.WorldBorder.IsWithinBounds(pos))
                return false;
            if (state.Owner.IsAir) return true;
            var path = state.Owner.Id.Path;
            if (path is "obsidian" or "crying_obsidian" or "respawn_anchor" or "reinforced_deepslate")
                return false;
            if (direction == Direction.Down && pos.Y == level.MinBuildHeight) return false;
            if (direction == Direction.Up && pos.Y == level.MaxBuildHeight - 1) return false;
            if (path is "piston" or "sticky_piston")
            {
                //An extending piston cannot be pushed itself, a retracted one counts as a normal block
                if (state.GetValue(BlockStateProperties.Extended)) return false;
            }
            else
            {
                if (state.Owner is not BlockBehaviour behaviour) return false;
                if (behaviour.DestroySpeed == -1.0f) return false;
                switch (PistonPushReactions.Of(state))
                {
                    case PushReaction.block:
                        return false;
                    case PushReaction.destroy:
                        return allowDestroyable;
                    case PushReaction.push_only:
                        return direction == connectionDirection;
                }
            }
            //Blocks with a block entity are never pushable
            return state.Owner is not BlockBehaviour blockBehaviour || !blockBehaviour.HasBlockEntity;
        }

        //MoveBlocks performs one move from the resolution result, maps to vanilla moveBlocks
        //Order: break what should break -> replace blocks with moving pistons far to near -> add the piston head when extending -> clear vacated cells -> refresh neighbors all at once
        private bool MoveBlocks(ServerLevel level, BlockPos pistonPos, Direction direction, bool extending)
        {
            var armPos = pistonPos.Offset(direction);
            if (!extending && level.GetBlockState(armPos)?.Owner.Id.Path == "piston_head")
                level.SetBlock(armPos, Blocks.AIR.DefaultBlockState, 276);
            var resolver = new PistonStructureResolver(level, pistonPos, direction, extending);
            if (!resolver.Resolve()) return false;

            //A pushed-away cell must be cleared once emptied, but not if another block filled it again
            var deleteAfterMove = new Dictionary<BlockPos, BlockState>();
            var toPushStates = new List<BlockState>();
            foreach (var pos in resolver.ToPush)
            {
                var state = level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;
                toPushStates.Add(state);
                deleteAfterMove[pos] = state;
            }
            var toDestroy = resolver.ToDestroy;
            var toUpdate = new BlockState[resolver.ToPush.Count + toDestroy.Count];
            var pushDirection = extending ? direction : direction.Opposite;
            var updateIndex = 0;
            for (var i = toDestroy.Count - 1; i >= 0; i--)
            {
                var pos = toDestroy[i];
                var blockState = level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;
                DropResources(level, pos, blockState);
                level.SetBlock(pos, Blocks.AIR.DefaultBlockState, 18);
                toUpdate[updateIndex++] = blockState;
            }
            for (var i = resolver.ToPush.Count - 1; i >= 0; i--)
            {
                var pos = resolver.ToPush[i];
                var blockState = level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;
                var destination = pos.Offset(pushDirection);
                deleteAfterMove.Remove(destination);
                var movingState = MOVING_PISTON.DefaultBlockState
                    .SetValue(BlockStateProperties.FacingProperty, direction.ToState());
                level.SetBlock(destination, movingState, 324);
                level.SetBlockEntity(new PistonMovingBlockEntity(destination, movingState, toPushStates[i],
                    direction, extending, false));
                toUpdate[updateIndex++] = blockState;
            }
            if (extending)
            {
                var headState = PISTON_HEAD.DefaultBlockState
                    .SetValue(BlockStateProperties.FacingProperty, direction.ToState())
                    .SetValue(BlockStateProperties.PistonTypeProperty,
                        _isSticky ? PistonType.sticky : PistonType.normal);
                var movingState = MOVING_PISTON.DefaultBlockState
                    .SetValue(BlockStateProperties.FacingProperty, direction.ToState())
                    .SetValue(BlockStateProperties.PistonTypeProperty,
                        _isSticky ? PistonType.sticky : PistonType.normal);
                deleteAfterMove.Remove(armPos);
                level.SetBlock(armPos, movingState, 324);
                level.SetBlockEntity(new PistonMovingBlockEntity(armPos, movingState, headState, direction,
                    true, true));
            }
            var air = Blocks.AIR.DefaultBlockState;
            foreach (var pos in deleteAfterMove.Keys) level.SetBlock(pos, air, 82);
            foreach (var (pos, oldState) in deleteAfterMove)
            {
                BlockUpdateHelper.UpdateIndirectNeighbourShapes(level, oldState, pos, 2,
                    BlockUpdateFlags.UpdateLimitDefault);
                BlockUpdateHelper.UpdateNeighbourShapes(level, air, pos, 2,
                    BlockUpdateFlags.UpdateLimitDefault);
                BlockUpdateHelper.UpdateIndirectNeighbourShapes(level, air, pos, 2,
                    BlockUpdateFlags.UpdateLimitDefault);
            }
            updateIndex = 0;
            for (var i = toDestroy.Count - 1; i >= 0; i--)
            {
                var state = toUpdate[updateIndex++];
                var pos = toDestroy[i];
                if (state.Owner is BlockBehaviour behaviour)
                    behaviour.AffectNeighborsAfterRemoval(level, pos, state, false);
                BlockUpdateHelper.UpdateIndirectNeighbourShapes(level, state, pos, 2,
                    BlockUpdateFlags.UpdateLimitDefault);
                level.UpdateNeighborsAt(pos, state.Owner);
            }
            for (var i = resolver.ToPush.Count - 1; i >= 0; i--)
                level.UpdateNeighborsAt(resolver.ToPush[i], toUpdate[updateIndex++].Owner);
            if (extending) level.UpdateNeighborsAt(armPos, PISTON_HEAD);
            return true;
        }

        //DropResources generates drops from the block's own loot table, maps to vanilla dropResources
        private static void DropResources(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.Owner is not BlockBehaviour behaviour) return;
            //Drops must go into the entity manager, which only a persistent level has
            if (level is not PersistentServerLevel persistent) return;
            foreach (var drop in behaviour.GetDrops(persistent, null, pos, state))
                ServerBlockUpdates.SpawnDrop(persistent, pos, drop);
        }
    }

    //MovingPistonBlock moving piston, the temporary block standing in for a pushed block during a push, maps to vanilla MovingPistonBlock
    //It has no shape of its own, collision and appearance all come from the block entity at the same position
    public sealed class MovingPistonBlock : NamedBlock
    {
        public MovingPistonBlock(string name) : base(name) { }

        //HasBlockEntity the pushed block's state lives in the block entity
        public override bool HasBlockEntity => true;

        //CreateBlockEntity the entity is passed in by the piston with parameters, not created from the state
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => null;

        //Destroy cleans up the leftover extended piston in the opposite cell when this is displaced, maps to vanilla destroy
        //The moving piston is one with the piston base; when the cell reverts to the real block at the end of a move the base must retract too
        public override void Destroy(ServerLevel level, BlockPos pos, BlockState state)
        {
            var relative = pos.Offset(state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive().Opposite);
            var neighbour = level.GetBlockState(relative);
            if (neighbour is not { } neighbourState
                || neighbourState.Owner.Id.Path is not ("piston" or "sticky_piston")
                || !neighbourState.GetValue(BlockStateProperties.Extended))
                return;
            level.SetBlock(relative, Blocks.AIR.DefaultBlockState, BlockUpdateFlags.All);
        }

        //UseOn right clicking a moving piston with no block entity clears it directly, maps to vanilla useWithoutItem
        //An empty shell left by an interrupted move cannot disappear on its own and vanilla leaves this manual cleanup path
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (level.GetBlockEntity<PistonMovingBlockEntity>(pos) is not null) return false;
            level.SetBlock(pos, Blocks.AIR.DefaultBlockState, BlockUpdateFlags.All);
            return true;
        }

        //GetDrops drops come from the moved block, maps to vanilla getDrops
        //Without the override, mining it mid-move would drop the moving piston itself
        public override IEnumerable<ItemStack> GetDrops(ServerLevel level, ServerPlayer? player, BlockPos pos,
            BlockState state)
        {
            if (level.GetBlockEntity<PistonMovingBlockEntity>(pos) is not { } entity) return [];
            return entity.MovedState.Owner is BlockBehaviour behaviour
                ? behaviour.GetDrops(level, player, pos, entity.MovedState)
                : [];
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => Shapes.Empty();

        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => level is ServerLevel serverLevel
                ? serverLevel.GetBlockEntity<PistonMovingBlockEntity>(pos)?.GetCollisionShape(level, pos)
                    ?? Shapes.Empty()
                : Shapes.Empty();

        //IsRedstoneConductor a moving piston is also not a redstone conductor, maps to vanilla isRedstoneConductor(Blocks::never)
        //Its collision shape comes from the moved block, usually a full block, so without turning it off it hits the same pitfall as the piston base
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;
    }
}
