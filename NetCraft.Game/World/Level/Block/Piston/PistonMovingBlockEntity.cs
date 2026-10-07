using NetCraft.Game.World.Phys.Collision;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
//Direction and Axis exist in both Primitives and Registry.Enums; the one used for blocks is always taken here
using Axis = NetCraft.Primitives.Direction.Axis;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonMovingBlockEntity the block entity standing in for a pushed block during a move, maps to the vanilla class of the same name
//It remembers the pushed block's original state and motion parameters, advances half a block per tick and finishes in two, pushing blocking entities out of the way
public sealed class PistonMovingBlockEntity : BlockEntity
{
    //PushOffset push compensation so entities land exactly outside the block, maps to vanilla PUSH_OFFSET
    private const double PushOffset = 0.01;

    //TickMovement progress per tick, maps to the hardcoded +0.5f in the vanilla tick
    //The vanilla TICK_MOVEMENT(0.51) constant is defined but referenced nowhere, so it cannot be used as the advance amount
    public const double TickMovement = 0.5;

    private static readonly BlockState DefaultMovedState = Blocks.AIR.DefaultBlockState;

    //Noclip the direction this piston is currently pushing along; the pushing shape does not take part in collisions during it, maps to vanilla NOCLIP
    private static readonly ThreadLocal<Direction?> Noclip = new();

    private BlockState _movedState = DefaultMovedState;
    private Direction _direction = Direction.Down;
    private bool _extending;
    private bool _isSourcePiston;
    private float _progress;
    private float _progressO;
    private long _lastTicked;

    public PistonMovingBlockEntity(BlockPos pos) : base(BlockEntityTypes.PISTON, pos) { }

    public PistonMovingBlockEntity(BlockPos pos, BlockState blockState, BlockState movedState,
        Direction direction, bool extending, bool isSourcePiston) : this(pos)
    {
        _movedState = movedState;
        _direction = direction;
        _extending = extending;
        _isSourcePiston = isSourcePiston;
    }

    //IsExtending whether this entity is extending or retracting
    public bool IsExtending => _extending;

    //MoveDirection the piston's own facing
    public Direction MoveDirection => _direction;

    //IsSourcePiston whether this is the segment extended by the piston base itself, which becomes the piston head shape on retract
    public bool IsSourcePiston => _isSourcePiston;

    //MovedState original state of the pushed block
    public BlockState MovedState => _movedState;

    //LastTicked the tick of the last advance, used by the piston for the retract event
    public long LastTicked => _lastTicked;

    //MovementDirection the direction the block is actually pushed, opposite the piston facing on retract
    public Direction MovementDirection => _extending ? _direction : _direction.Opposite;

    //PushDirection same as MovementDirection, maps to vanilla getPushDirection
    public Direction PushDirection => MovementDirection;

    //GetProgress progress interpolation, maps to vanilla getProgress
    public float GetProgress(float partial) => partial >= 1.0f ? _progress : _progressO + (_progress - _progressO) * partial;

    //Tick advances the animation, maps to the static vanilla tick
    public override void Tick()
    {
        if (Level is not { } level) return;
        _lastTicked = level.GameTime;
        _progressO = _progress;
        if (_progressO >= 1.0f)
        {
            //The push finished, replace the moving block with the real pushed block
            //First confirm this cell is still a moving piston, then detach the block entity
            //Vanilla does it the other way around; it can because block entities hang off the chunk object, so as long as the chunk is in memory the block can be read
            //Here block entities hang off a collection held globally per level and reading a block gives null when the chunk is missing
            //Without changing the order, in that case the entity would be detached first and then it returns, leaving the moving piston unattended
            //This shows up as a permanently stuck moving piston after a reload; with the base also settled it looks like a headless piston
            if (level.GetBlockState(Pos) is not { } current)
            {
                //The chunk is not in memory, so both advancing and detaching are out of the question; it is left until the chunk returns
                //Reaching this means the block entity collection holds a leftover whose chunk is no longer in memory; it is logged to help diagnose
                Log.Warning($"Chunk holding a moving piston is not loaded, skipping this tick {Pos}");
                return;
            }
            if (current.Owner.Id.Path != "moving_piston") return;
            level.RemoveBlockEntity(Pos);
            var finalState = UpdateFromNeighbourShapes(_movedState, level, Pos);
            if (finalState.Owner.IsAir)
            {
                level.SetBlock(Pos, _movedState, 340);
                BlockUpdateHelper.UpdateOrDestroy(_movedState, finalState, level, Pos, 3,
                    BlockUpdateFlags.UpdateLimitDefault);
            }
            else
            {
                if (finalState.HasProperty(BlockStateProperties.Waterlogged)
                    && finalState.GetValue(BlockStateProperties.Waterlogged))
                    finalState = finalState.SetValue(BlockStateProperties.Waterlogged, false);
                level.SetBlock(Pos, finalState, 67);
                level.NeighborChanged(Pos, finalState.Owner);
            }
            return;
        }
        var newProgress = _progress + (float)TickMovement;
        MoveCollidedEntities(level, Pos, newProgress, this);
        MoveStuckEntities(level, Pos, newProgress, this);
        _progress = newProgress >= 1.0f ? 1.0f : newProgress;
    }

    //FinalTick settles an unfinished animation immediately, maps to vanilla finalTick
    //On retract the previous segment is settled before placing the new block; without settling a phantom block is left behind
    public void FinalTick()
    {
        if (Level is not { } level) return;
        if (_progressO >= 1.0f) return;
        //Same rule as the cleanup in Tick: confirm the block is still a moving piston before touching the block entity
        //Returns directly when the block cannot be read, and must not detach the entity leaving an unattended moving piston
        if (level.GetBlockState(Pos)?.Owner.Id.Path != "moving_piston") return;
        _progressO = _progress = 1.0f;
        level.RemoveBlockEntity(Pos);
        var finalState = _isSourcePiston
            ? Blocks.AIR.DefaultBlockState
            : UpdateFromNeighbourShapes(_movedState, level, Pos);
        level.SetBlock(Pos, finalState, 3);
        level.NeighborChanged(Pos, finalState.Owner);
    }

    //OnRemoved settles the animation before the block entity leaves the world, maps to vanilla preRemoveSideEffects
    public override void OnRemoved() => FinalTick();

    //GetCollisionShape collision shape of a moving block, maps to the vanilla method of the same name
    //The piston head segment gets out of the way during the push so it does not jam on the very entity it pushes
    public VoxelShape GetCollisionShape(BlockGetter level, BlockPos pos)
    {
        var pistonHeadShape = !_extending && _isSourcePiston && IsPistonBase(_movedState)
            ? _movedState.SetValue(BlockStateProperties.Extended, true)
                .GetCollisionShape(level, pos, CollisionContext.Empty)
            : Shapes.Empty();
        if (_progress < 1.0 && Noclip.Value is { } noClip && noClip == MovementDirection)
            return pistonHeadShape;
        var blockState = _isSourcePiston
            ? Blocks.PISTON_HEAD.DefaultBlockState
                .SetValue(BlockStateProperties.FacingProperty, _direction.ToState())
                .SetValue(BlockStateProperties.Short, _extending != 1.0f - _progress < 0.25f)
            : _movedState;
        var extendedProgress = ExtendedProgress(_progress);
        return Shapes.Or(pistonHeadShape,
            blockState.GetCollisionShape(level, pos, CollisionContext.Empty)
                .Move(_direction.StepX * extendedProgress, _direction.StepY * extendedProgress,
                    _direction.StepZ * extendedProgress));
    }

    public override void SaveAdditional(CompoundTag tag)
    {
        tag.PutInt("blockState", _movedState.Id);
        tag.PutInt("facing", _direction.Id3D);
        tag.PutFloat("progress", _progressO);
        tag.PutBoolean("extending", _extending);
        tag.PutBoolean("source", _isSourcePiston);
    }

    public override void LoadAdditional(CompoundTag tag)
    {
        _movedState = BlockStateRegistry.GetState(tag.GetIntOr("blockState", 0));
        _direction = Direction.ById(tag.GetIntOr("facing", 0));
        _progressO = _progress = tag.GetFloatValue("progress");
        _extending = tag.GetBooleanOr("extending", false);
        _isSourcePiston = tag.GetBooleanOr("source", false);
    }

    //CollisionRelatedBlockState the state taking part in collision, using the piston head instead of the base on retract, maps to vanilla getCollisionRelatedBlockState
    private BlockState CollisionRelatedBlockState
        => !_extending && _isSourcePiston && IsPistonBase(_movedState)
            ? Blocks.PISTON_HEAD.DefaultBlockState
                .SetValue(BlockStateProperties.Short, _progress > 0.25f)
                .SetValue(BlockStateProperties.PistonTypeProperty,
                    _movedState.Owner.Id.Path == "sticky_piston" ? PistonType.sticky : PistonType.normal)
                .SetValue(BlockStateProperties.FacingProperty,
                    _movedState.GetValue(BlockStateProperties.FacingProperty))
            : _movedState;

    //IsPistonBase whether it is the piston base, the piston head segment is distinguished by it
    private static bool IsPistonBase(BlockState state)
        => state.Owner.Id.Path is "piston" or "sticky_piston";

    //ExtendedProgress converts progress into a displacement ratio, from -1 to 0 when extending and 0 to 1 when retracting
    private float ExtendedProgress(float progress) => _extending ? progress - 1.0f : 1.0f - progress;

    //MoveCollidedEntities pushes collided entities out, maps to the vanilla method of the same name
    private static void MoveCollidedEntities(ServerLevel level, BlockPos pos, float newProgress,
        PistonMovingBlockEntity self)
    {
        if (LevelView(level) is not { } view) return;
        var movement = self.MovementDirection;
        var deltaProgress = newProgress - self._progress;
        var shape = self.CollisionRelatedBlockState.GetCollisionShape(view, pos, CollisionContext.Empty);
        if (shape.IsEmpty) return;
        var box = MoveByPositionAndProgress(pos, shape.Bounds(), self);
        var sweep = PistonMath.GetMovementArea(box, movement, deltaProgress);
        var queryBox = new AABB(
            Math.Min(sweep.Min.X, box.Min.X), Math.Min(sweep.Min.Y, box.Min.Y), Math.Min(sweep.Min.Z, box.Min.Z),
            Math.Max(sweep.Max.X, box.Max.X), Math.Max(sweep.Max.Y, box.Max.Y), Math.Max(sweep.Max.Z, box.Max.Z));
        var entities = level.EntitiesInBox(queryBox).ToList();
        if (entities.Count == 0) return;
        var shapeAabbs = shape.ToAabbs();
        //Slime blocks fling entities rather than push them; there are no players in the set, so the vanilla player exemption does not apply here
        var causeBounce = self._movedState.Owner.Id.Path == "slime_block";
        foreach (var entity in entities)
        {
            if (causeBounce)
            {
                var velocity = entity.Velocity;
                var dx = velocity.X;
                var dy = velocity.Y;
                var dz = velocity.Z;
                var axis = movement.GetAxis();
                if (axis == Axis.X) dx = movement.StepX;
                else if (axis == Axis.Y) dy = movement.StepY;
                else if (axis == Axis.Z) dz = movement.StepZ;
                entity.Velocity = new Vec3(dx, dy, dz);
            }
            var delta = 0.0;
            foreach (var shapeAabb in shapeAabbs)
            {
                var movingAabb = PistonMath.GetMovementArea(
                    MoveByPositionAndProgress(pos, shapeAabb, self), movement, deltaProgress);
                var entityBox = entity.BoundingBox;
                if (!movingAabb.Intersects(entityBox)) continue;
                delta = Math.Max(delta, GetMovement(movingAabb, movement, entityBox));
                if (delta >= deltaProgress) break;
            }
            if (delta <= 0.0) continue;
            delta = Math.Min(delta, deltaProgress) + PushOffset;
            MoveEntityByPiston(movement, entity, delta, movement);
            if (self._extending || !self._isSourcePiston) continue;
            FixEntityWithinPistonBase(pos, entity, movement, deltaProgress);
        }
    }

    //MoveStuckEntities entities stuck to a honey block's side are carried along, maps to vanilla moveStuckEntities
    private static void MoveStuckEntities(ServerLevel level, BlockPos pos, float newProgress,
        PistonMovingBlockEntity self)
    {
        if (self._movedState.Owner.Id.Path != "honey_block") return;
        if (LevelView(level) is not { } view) return;
        var movement = self.MovementDirection;
        if (!movement.IsHorizontal) return;
        var stickyTop = self._movedState.GetCollisionShape(view, pos, CollisionContext.Empty).Max(Axis.Y);
        var box = MoveByPositionAndProgress(pos, new AABB(0.0, stickyTop, 0.0, 1.0, 1.5000010000000001, 1.0), self);
        var deltaProgress = newProgress - self._progress;
        foreach (var entity in level.EntitiesInBox(box).Where(entity => MatchesStickyCriteria(box, entity)).ToList())
            MoveEntityByPiston(movement, entity, deltaProgress, movement);
    }

    //MatchesStickyCriteria carried by a honey block only when grounded and within the block range, maps to vanilla matchesStickyCritera
    private static bool MatchesStickyCriteria(AABB box, NetCraft.Registry.Entity entity)
        => entity.OnGround
            && entity.Pos.X >= box.Min.X && entity.Pos.X <= box.Max.X
            && entity.Pos.Z >= box.Min.Z && entity.Pos.Z <= box.Max.Z;

    //MoveEntityByPiston pushes the entity delta blocks along movement, maps to vanilla moveEntityByPiston
    private static void MoveEntityByPiston(Direction pistonDirection, NetCraft.Registry.Entity entity,
        double delta, Direction movement)
    {
        Noclip.Value = pistonDirection;
        entity.Move(new Vec3(delta * movement.StepX, delta * movement.StepY, delta * movement.StepZ));
        Noclip.Value = null;
    }

    //GetMovement how much the entity must move to get out of the given box, maps to vanilla getMovement
    private static double GetMovement(AABB boxToBeOutsideOf, Direction movement, AABB box)
    {
        if (movement == Direction.East) return boxToBeOutsideOf.Max.X - box.Min.X;
        if (movement == Direction.West) return box.Max.X - boxToBeOutsideOf.Min.X;
        if (movement == Direction.Down) return box.Max.Y - boxToBeOutsideOf.Min.Y;
        if (movement == Direction.South) return boxToBeOutsideOf.Max.Z - box.Min.Z;
        if (movement == Direction.North) return box.Max.Z - boxToBeOutsideOf.Min.Z;
        return boxToBeOutsideOf.Max.Y - box.Min.Y;
    }

    //MoveByPositionAndProgress translates the block box into world coordinates by the current progress, maps to vanilla moveByPositionAndProgress
    private static AABB MoveByPositionAndProgress(BlockPos pos, AABB box, PistonMovingBlockEntity entity)
    {
        var current = entity.ExtendedProgress(entity._progress);
        return box.Move(new Vec3(
            pos.X + current * entity._direction.StepX,
            pos.Y + current * entity._direction.StepY,
            pos.Z + current * entity._direction.StepZ));
    }

    //FixEntityWithinPistonBase pushes entities trapped inside the base back out on retract, maps to vanilla fixEntityWithinPistonBase
    private static void FixEntityWithinPistonBase(BlockPos pos, NetCraft.Registry.Entity entity,
        Direction direction, double deltaProgress)
    {
        var entityBox = entity.BoundingBox;
        var baseBox = Shapes.Block().Bounds().Move(new Vec3(pos.X, pos.Y, pos.Z));
        if (!entityBox.Intersects(baseBox)) return;
        var opposite = direction.Opposite;
        var delta = GetMovement(baseBox, opposite, entityBox) + PushOffset;
        var overlapped = Intersect(entityBox, baseBox);
        var deltaOverlapped = GetMovement(baseBox, opposite, overlapped) + PushOffset;
        if (Math.Abs(delta - deltaOverlapped) >= PushOffset) return;
        delta = Math.Min(delta, deltaProgress) + PushOffset;
        MoveEntityByPiston(direction, entity, delta, opposite);
    }

    //Intersect intersection of two boxes, negative size when empty, same as vanilla AABB.intersect
    private static AABB Intersect(AABB first, AABB second)
        => new(
            Math.Max(first.Min.X, second.Min.X), Math.Max(first.Min.Y, second.Min.Y),
            Math.Max(first.Min.Z, second.Min.Z), Math.Min(first.Max.X, second.Max.X),
            Math.Min(first.Max.Y, second.Max.Y), Math.Min(first.Max.Z, second.Max.Z));

    //UpdateFromNeighbourShapes runs updateShape across six directions so the moved state adapts to its new neighbors, maps to vanilla Block.updateFromNeighbourShapes
    private static BlockState UpdateFromNeighbourShapes(BlockState state, ServerLevel level, BlockPos pos)
    {
        var result = state;
        foreach (var direction in BlockUpdateFlags.ShapeUpdateOrder)
        {
            var neighbourPos = pos.Offset(direction);
            if (level.GetBlockState(neighbourPos) is not { } neighbour) continue;
            if (result.Owner is not BlockBehaviour behaviour) continue;
            result = behaviour.UpdateShape(level, pos, result, direction, neighbourPos, neighbour);
        }
        return result;
    }

    //LevelView collision query view; the section range is unavailable when the level is not a persistent server level
    private static BlockGetter? LevelView(ServerLevel level)
        => level is PersistentServerLevel persistent
            ? new LevelCollisionGetter(persistent, persistent.MinSectionY, persistent.SectionsCount)
            : null;
}
