using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;
using StateDirection = NetCraft.Registry.Enums.Direction;

namespace NetCraft.Game.World.Level.Block;

//Blocks 观察者部分 与 Blocks.cs 同一个类分开文件免得主文件太长
public static partial class Blocks
{
    //ObserverBlock 观察者 对应原版 ObserverBlock
    //眼睛朝 FACING 那一面盯着 那边一变就发一次两刻脉冲
    //输出在 FACING 的反向 也就是眼睛看进去的背面
    public sealed class ObserverBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("observer");

        //原版观察者硬度 3 需要镐子
        public override float DestroySpeed => 3f;
        public override bool RequiresCorrectToolForDrops => true;

        //观察者不是导体 原版注册时显式给了 isRedstoneConductor(Blocks::never)
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.FacingProperty,
            ["powered"] = BlockStateProperties.Powered,
        };

        //原版观察者默认态 FACING=south POWERED=false
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.FacingProperty, StateDirection.south)
                .SetValue(BlockStateProperties.Powered, false);

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        //GetDirectSignal 与自身信号一致 对应原版覆写
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => GetSignal(level, pos, state, direction);

        //GetSignal 只朝 FACING 的反向输出 对应原版覆写
        //查询者在方向反侧 与二极管同一套语义
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive() == direction
                ? OwnSignal(level, pos, state)
                : 0;

        //GetStateForPlacement 眼睛朝玩家视线最近的那个方向 对应原版两次 getOpposite 抵消后的结果
        //原版这里写的是 getNearestLookingDirection().getOpposite().getOpposite() 两次取反等于自身
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState.SetValue(BlockStateProperties.FacingProperty, lookingDirection.ToState());

        //Tick 通电时到点关掉 断电时打开并排两刻 两次各自都通知输出侧 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Powered))
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, false),
                    BlockUpdateFlags.Clients);
            }
            else
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, true),
                    BlockUpdateFlags.Clients);
                level.ScheduleTick(pos, this, 2);
            }
            UpdateNeighborsInFront(level, pos, state);
        }

        //UpdateShape 被盯的那一面形状变了就起脉冲 对应原版 updateShape
        //观察者就是靠形状更新通道感知被观察方块变化的
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive() == directionToNeighbour
                && !state.GetValue(BlockStateProperties.Powered))
                StartSignal(level, pos);
            return state;
        }

        //StartSignal 还没排刻就排两刻 对应原版 startSignal
        private void StartSignal(ServerLevel level, BlockPos pos)
        {
            if (!level.HasScheduledTick(pos, this)) level.ScheduleTick(pos, this, 2);
        }

        //OnPlace 换上的观察者如果停在通电态且没排刻 直接熄掉再通知输出侧 对应原版 onPlace
        //写回用 KnownShape 让这一步不再触发形状更新 免得刚放上就自己把自己点起来
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            if (!state.GetValue(BlockStateProperties.Powered) || level.HasScheduledTick(pos, this)) return;
            var cleared = state.SetValue(BlockStateProperties.Powered, false);
            level.SetBlock(pos, cleared, BlockUpdateFlags.KnownShape | BlockUpdateFlags.Clients);
            UpdateNeighborsInFront(level, pos, cleared);
        }

        //AffectNeighborsAfterRemoval 脉冲还没走完就被拆掉 输出侧也要跟着归零 对应原版同名方法
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (state.GetValue(BlockStateProperties.Powered) && level.HasScheduledTick(pos, this))
                UpdateNeighborsInFront(level, pos, state.SetValue(BlockStateProperties.Powered, false));
        }

        //UpdateNeighborsInFront 通知输出侧那格及其邻接 对应原版同名方法
        private void UpdateNeighborsInFront(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.FacingProperty).ToPrimitive();
            var backPos = pos.Offset(direction.Opposite);
            level.NeighborChanged(backPos, this);
            level.UpdateNeighborsAtExceptFromFacing(backPos, this, direction);
        }
    }

    public static readonly ObserverBlock OBSERVER = new();
}
