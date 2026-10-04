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
//方向与轴同时存在于 Primitives 与 Registry.Enums 这里一律取方块用的那套
using Axis = NetCraft.Primitives.Direction.Axis;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonMovingBlockEntity 推动过程中顶替被推方块的那个方块实体 对应原版同名类
//它记住被推方块的原状态与运动参数 每刻推进半格 两刻推完 期间把挡路的实体一起挤走
public sealed class PistonMovingBlockEntity : BlockEntity
{
    //PushOffset 挤压补偿量 让实体刚好落在方格外侧 对应原版 PUSH_OFFSET
    private const double PushOffset = 0.01;

    //TickMovement 每刻推进的进度 对应原版 tick 里写死的 +0.5f
    //原版那个 TICK_MOVEMENT(0.51) 常量定义了却没有被任何地方引用 不能拿它当推进量
    public const double TickMovement = 0.5;

    private static readonly BlockState DefaultMovedState = Blocks.AIR.DefaultBlockState;

    //Noclip 正在被本活塞推着走的方向 这期间推人那形状不参与碰撞 对应原版 NOCLIP
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

    //IsExtending 本实体是在伸出还是收回
    public bool IsExtending => _extending;

    //MoveDirection 活塞自身的朝向
    public Direction MoveDirection => _direction;

    //IsSourcePiston 是否由活塞底座自己伸出的那截 收回时它换成活塞头形状
    public bool IsSourcePiston => _isSourcePiston;

    //MovedState 被推方块的原状态
    public BlockState MovedState => _movedState;

    //LastTicked 上次推进的刻 活塞判收回事件用
    public long LastTicked => _lastTicked;

    //MovementDirection 方块实际被推走的方向 收回时与活塞朝向相反
    public Direction MovementDirection => _extending ? _direction : _direction.Opposite;

    //PushDirection 与 MovementDirection 同义 对应原版 getPushDirection
    public Direction PushDirection => MovementDirection;

    //GetProgress 进度插值 对应原版 getProgress
    public float GetProgress(float partial) => partial >= 1.0f ? _progress : _progressO + (_progress - _progressO) * partial;

    //Tick 推进动画 对应原版 tick 的静态方法
    public override void Tick()
    {
        if (Level is not { } level) return;
        _lastTicked = level.GameTime;
        _progressO = _progress;
        if (_progressO >= 1.0f)
        {
            //推完了 把移动方块换回被推方块的真身
            //先确认这一格还是移动活塞 再摘方块实体
            //原版是反过来的 它能这么写是因为方块实体挂在区块对象上 区块在内存就读得到方块
            //本作方块实体挂在按关卡全局持有的集合里 读方块在区块缺失时给 null
            //顺序不改的话那种情况下会先摘掉实体再直接返回 那格移动活塞就再也没人管
            //表现就是重开后永久卡死的移动活塞 底座那格同时落定的话看着就是无头活塞
            if (level.GetBlockState(Pos) is not { } current)
            {
                //区块不在内存 这时推进与摘实体都无从谈起 留着等区块回来再走完
                //真出现这条说明方块实体集合里有区块已经不在内存的残留 记下来便于定位
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

    //FinalTick 立刻把没走完的动画落地 对应原版 finalTick
    //收回时先把上一截收尾再摆新方块 不落地就会留下一格假方块
    public void FinalTick()
    {
        if (Level is not { } level) return;
        if (_progressO >= 1.0f) return;
        //与 Tick 的收尾同一条规矩: 先确认方块还是移动活塞再动方块实体
        //读不到方块时直接返回 不能把实体摘掉留一格没人管的移动活塞
        if (level.GetBlockState(Pos)?.Owner.Id.Path != "moving_piston") return;
        _progressO = _progress = 1.0f;
        level.RemoveBlockEntity(Pos);
        var finalState = _isSourcePiston
            ? Blocks.AIR.DefaultBlockState
            : UpdateFromNeighbourShapes(_movedState, level, Pos);
        level.SetBlock(Pos, finalState, 3);
        level.NeighborChanged(Pos, finalState.Owner);
    }

    //OnRemoved 方块实体被移出世界时先把动画落地 对应原版 preRemoveSideEffects
    public override void OnRemoved() => FinalTick();

    //GetCollisionShape 移动中方块的碰撞形状 对应原版同名方法
    //活塞头那截在推进期间让开 免得被自己推的实体卡住
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

    //CollisionRelatedBlockState 参与碰撞计算的状态 收回时用活塞头代替底座 对应原版 getCollisionRelatedBlockState
    private BlockState CollisionRelatedBlockState
        => !_extending && _isSourcePiston && IsPistonBase(_movedState)
            ? Blocks.PISTON_HEAD.DefaultBlockState
                .SetValue(BlockStateProperties.Short, _progress > 0.25f)
                .SetValue(BlockStateProperties.PistonTypeProperty,
                    _movedState.Owner.Id.Path == "sticky_piston" ? PistonType.sticky : PistonType.normal)
                .SetValue(BlockStateProperties.FacingProperty,
                    _movedState.GetValue(BlockStateProperties.FacingProperty))
            : _movedState;

    //IsPistonBase 是否是活塞底座 活塞头那截要靠它区分
    private static bool IsPistonBase(BlockState state)
        => state.Owner.Id.Path is "piston" or "sticky_piston";

    //ExtendedProgress 进度换算成位移比例 伸出时从 -1 走到 0 收回时从 0 走到 1
    private float ExtendedProgress(float progress) => _extending ? progress - 1.0f : 1.0f - progress;

    //MoveCollidedEntities 把推到的实体挤出去 对应原版同名方法
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
        //粘液块不推而是把实体甩出去 集合里没有玩家 原版对玩家的豁免在这里用不上
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

    //MoveStuckEntities 蜂蜜块侧壁粘着的实体跟着走 对应原版 moveStuckEntities
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

    //MatchesStickyCriteria 站得住又落在方块范围内才被蜂蜜块带走 对应原版 matchesStickyCritera
    private static bool MatchesStickyCriteria(AABB box, NetCraft.Registry.Entity entity)
        => entity.OnGround
            && entity.Pos.X >= box.Min.X && entity.Pos.X <= box.Max.X
            && entity.Pos.Z >= box.Min.Z && entity.Pos.Z <= box.Max.Z;

    //MoveEntityByPiston 沿 movement 把实体推走 delta 格 对应原版 moveEntityByPiston
    private static void MoveEntityByPiston(Direction pistonDirection, NetCraft.Registry.Entity entity,
        double delta, Direction movement)
    {
        Noclip.Value = pistonDirection;
        entity.Move(new Vec3(delta * movement.StepX, delta * movement.StepY, delta * movement.StepZ));
        Noclip.Value = null;
    }

    //GetMovement 实体要挪出指定盒外还差多少 对应原版 getMovement
    private static double GetMovement(AABB boxToBeOutsideOf, Direction movement, AABB box)
    {
        if (movement == Direction.East) return boxToBeOutsideOf.Max.X - box.Min.X;
        if (movement == Direction.West) return box.Max.X - boxToBeOutsideOf.Min.X;
        if (movement == Direction.Down) return box.Max.Y - boxToBeOutsideOf.Min.Y;
        if (movement == Direction.South) return boxToBeOutsideOf.Max.Z - box.Min.Z;
        if (movement == Direction.North) return box.Max.Z - boxToBeOutsideOf.Min.Z;
        return boxToBeOutsideOf.Max.Y - box.Min.Y;
    }

    //MoveByPositionAndProgress 方块盒按当前进度平移到世界坐标 对应原版 moveByPositionAndProgress
    private static AABB MoveByPositionAndProgress(BlockPos pos, AABB box, PistonMovingBlockEntity entity)
    {
        var current = entity.ExtendedProgress(entity._progress);
        return box.Move(new Vec3(
            pos.X + current * entity._direction.StepX,
            pos.Y + current * entity._direction.StepY,
            pos.Z + current * entity._direction.StepZ));
    }

    //FixEntityWithinPistonBase 收回时把夹在底座里的实体顶回底座外 对应原版 fixEntityWithinPistonBase
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

    //Intersect 两盒的交集 空集时宽高为负 与原版 AABB.intersect 一致
    private static AABB Intersect(AABB first, AABB second)
        => new(
            Math.Max(first.Min.X, second.Min.X), Math.Max(first.Min.Y, second.Min.Y),
            Math.Max(first.Min.Z, second.Min.Z), Math.Min(first.Max.X, second.Max.X),
            Math.Min(first.Max.Y, second.Max.Y), Math.Min(first.Max.Z, second.Max.Z));

    //UpdateFromNeighbourShapes 六向走一遍 updateShape 让搬过去的状态重新适应邻居 对应原版 Block.updateFromNeighbourShapes
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

    //LevelView 碰撞查询视图 关卡不是持久化服务端关卡时拿不到区段范围
    private static BlockGetter? LevelView(ServerLevel level)
        => level is PersistentServerLevel persistent
            ? new LevelCollisionGetter(persistent, persistent.MinSectionY, persistent.SectionsCount)
            : null;
}
