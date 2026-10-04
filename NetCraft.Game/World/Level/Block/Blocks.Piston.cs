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
//方向同时存在于 Primitives 与 Registry.Enums 这里取方块用的那套
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//P-1 活塞系列方块 对应原版 net.minecraft.world.level.block.piston 包
//底座负责收发信号与搬运 移动活塞顶替被推方块 活塞头是伸出后的那截
//推动结构解析见 Piston.PistonStructureResolver 动画与挤压见 Piston.PistonMovingBlockEntity
public static partial class Blocks
{
    public static readonly PistonBaseBlock PISTON = new("piston", false);
    public static readonly PistonBaseBlock STICKY_PISTON = new("sticky_piston", true);
    public static readonly MovingPistonBlock MOVING_PISTON = new("moving_piston");

    //RegisterPiston 活塞系列登记进真实方块表
    private static void RegisterPiston(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { PISTON, STICKY_PISTON, MOVING_PISTON };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //PistonBaseBlock 活塞底座 对应原版 PistonBaseBlock
    //通电伸出断电收回 两刻完成 伸出后自身只剩底座那截 活塞臂由 piston_head 承担
    public sealed class PistonBaseBlock : NamedBlock
    {
        //TriggerExtend 伸出事件 对应原版 TRIGGER_EXTEND
        public const int TriggerExtend = 0;

        //TriggerContract 收回事件 对应原版 TRIGGER_CONTRACT
        public const int TriggerContract = 1;

        //TriggerDrop 收回时直接丢下不搬运 对应原版 TRIGGER_DROP
        public const int TriggerDrop = 2;

        //伸出后底座只剩四像素厚的一条 其余交给活塞头
        private static readonly Dictionary<Direction, VoxelShape> ShapesExtended =
            Shapes.RotateAll(NetCraft.Registry.Block.BoxZ(16.0, 4.0, 16.0));

        private readonly bool _isSticky;

        public PistonBaseBlock(string name, bool isSticky) : base(name) => _isSticky = isSticky;

        //IsSticky 粘性活塞收回时把前方那格一起拉回来
        public bool IsSticky => _isSticky;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => state.GetValue(BlockStateProperties.Extended)
                ? ShapesExtended[state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive()]
                : Shapes.Block();

        //IsRedstoneConductor 活塞不是红石导体 对应原版 pistonProperties 里的 isRedstoneConductor(Blocks::never)
        //不关掉的话未伸出的整格碰撞形状会被判成导体
        //getSignal 的导体分支会把四周朝活塞的直接信号一并吸进来
        //贴在推动面上的按钮正好朝下给直接信号 活塞就自己伸出把自己推的按钮铲掉了 原版不会
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        //SetPlacedBy 放下时立刻按周围信号决定要不要伸出 对应原版 setPlacedBy
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
            => CheckIfExtend(level, pos, state);

        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston) => CheckIfExtend(level, pos, state);

        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            //同方块换状态不算放置 移动中的底座也不重新判定
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            if (level.GetBlockEntity<BlockEntity>(pos) is not null) return;
            CheckIfExtend(level, pos, state);
        }

        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState
                .SetValue(BlockStateProperties.FacingProperty, lookingDirection.Opposite.ToState())
                .SetValue(BlockStateProperties.Extended, false);

        //CheckIfExtend 信号变了就排一个方块事件 对应原版 checkIfExtend
        //真正搬运放在事件里做 这样与原版一样延迟一刻 同刻内的多次信号变化能合并
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
                //上一截还在半路时收回要降级成丢下 免得把没推完的方块留在原地
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

        //HasNeighborSignal 六向加自身上方那格的信号 推动方向那面自己不触发 对应原版 getNeighborSignal
        private static bool HasNeighborSignal(ServerLevel level, BlockPos pos, Direction pushDirection)
        {
            foreach (var direction in Direction.Values)
            {
                if (direction == pushDirection) continue;
                if (level.GetSignal(pos.Offset(direction), direction) > 0) return true;
            }
            //自身这格看朝向下方 再连上方一格及其六邻(除下) 这段就是原版的准连接性
            //半连接装置能隔着方块激活活塞靠的是它 与是不是红石导体无关
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
            //信号在这刻又变了 事件作废按原版重排
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
            //收回前把前方那截没走完的动画先落地
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
                    //前面那格能拖就拖回来 拖不动就直接删掉
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

        //IsPushable 该方块能不能被活塞挪走 对应原版静态方法
        //allowDestroyable 为真时允许"推之前先毁掉"的那类方块通过
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
                //伸出中的活塞自己推不动 没伸出的按普通方块算
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
            //带方块实体的方块一律推不动
            return state.Owner is not BlockBehaviour blockBehaviour || !blockBehaviour.HasBlockEntity;
        }

        //MoveBlocks 按解析结果完成一次搬运 对应原版 moveBlocks
        //顺序: 破坏该破坏的 -> 从远到近把方块换成移动活塞 -> 伸出时补活塞头 -> 清掉空出来的格子 -> 统一刷邻居
        private bool MoveBlocks(ServerLevel level, BlockPos pistonPos, Direction direction, bool extending)
        {
            var armPos = pistonPos.Offset(direction);
            if (!extending && level.GetBlockState(armPos)?.Owner.Id.Path == "piston_head")
                level.SetBlock(armPos, Blocks.AIR.DefaultBlockState, 276);
            var resolver = new PistonStructureResolver(level, pistonPos, direction, extending);
            if (!resolver.Resolve()) return false;

            //被推走的格子搬空后要清 但若又被别的方块填上就不清
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

        //DropResources 按方块自己的掉落表生成掉落物 对应原版 dropResources
        private static void DropResources(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.Owner is not BlockBehaviour behaviour) return;
            //掉落物要进实体管理器 只有持久化关卡有
            if (level is not PersistentServerLevel persistent) return;
            foreach (var drop in behaviour.GetDrops(persistent, null, pos, state))
                ServerBlockUpdates.SpawnDrop(persistent, pos, drop);
        }
    }

    //MovingPistonBlock 移动活塞 推动过程中顶替被推方块的临时方块 对应原版 MovingPistonBlock
    //它自己没有形状 碰撞与表现全由同位置的方块实体给
    public sealed class MovingPistonBlock : NamedBlock
    {
        public MovingPistonBlock(string name) : base(name) { }

        //HasBlockEntity 被推方块的状态存在方块实体里
        public override bool HasBlockEntity => true;

        //CreateBlockEntity 实体由活塞带着参数塞进来 不走按状态新建那条路
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => null;

        //Destroy 被顶替掉时收掉反方向那格残留的伸出态活塞 对应原版 destroy
        //移动活塞与活塞底座是一体的 搬运结束那格换回真方块时底座也得跟着收
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

        //UseOn 右键一个没有方块实体的移动活塞直接清掉 对应原版 useWithoutItem
        //半路被打断留下的空壳没法自己消失 原版留了这么一条手动收拾的路
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (level.GetBlockEntity<PistonMovingBlockEntity>(pos) is not null) return false;
            level.SetBlock(pos, Blocks.AIR.DefaultBlockState, BlockUpdateFlags.All);
            return true;
        }

        //GetDrops 掉落取被移动方块那一份 对应原版 getDrops
        //不覆写的话搬运途中被挖掉会掉出移动活塞本身
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

        //IsRedstoneConductor 搬运中的移动活塞同样不是红石导体 对应原版 isRedstoneConductor(Blocks::never)
        //它的碰撞形状取的是被搬运方块那份 多半是整格 不关掉会和活塞底座踩同一个坑
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;
    }
}
