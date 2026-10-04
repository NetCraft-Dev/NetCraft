using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//CollectingNeighborUpdater 收集式更新器 对应原版 CollectingNeighborUpdater
//单栈 + 延迟列表 + 重入计数 两条通道混在同一条队列里按同一套规则交错执行
//排空发生在调用栈内 不逐 tick 处理 也没有每 tick 配额
public sealed class CollectingNeighborUpdater : INeighborUpdater
{
    private readonly ServerLevel _level;
    //负数表示不限制链长
    private readonly int _maxChainedNeighborUpdates;
    //_stack 待执行项 LIFO
    private readonly Stack<INeighborUpdate> _stack = new();
    //_addedThisLayer 当前项执行期间新产生的更新 延后到当前项做完再压栈
    private readonly List<INeighborUpdate> _addedThisLayer = new();
    //_count 重入计数兼链长计数
    private int _count;

    public CollectingNeighborUpdater(ServerLevel level, int maxChainedNeighborUpdates)
    {
        _level = level;
        _maxChainedNeighborUpdates = maxChainedNeighborUpdates;
    }

    public void ShapeUpdate(Direction direction, BlockState neighbourState, BlockPos pos, BlockPos neighbourPos,
        int updateFlags, int updateLimit)
        => AddAndRun(pos, new ShapeUpdateTask(direction, neighbourState, pos, neighbourPos, updateFlags, updateLimit));

    public void NeighborChanged(BlockPos pos, NetCraft.Registry.Block changedBlock)
        => AddAndRun(pos, new SimpleNeighborUpdate(pos, changedBlock));

    public void NeighborChanged(BlockPos pos, BlockState state, NetCraft.Registry.Block changedBlock,
        bool movedByPiston)
        => AddAndRun(pos, new FullNeighborUpdate(state, pos, changedBlock, movedByPiston));

    public void UpdateNeighborsAtExceptFromFacing(BlockPos pos, NetCraft.Registry.Block block, Direction? skipDirection)
        => AddAndRun(pos, new MultiNeighborUpdate(pos, block, skipDirection));

    //AddAndRun 入队一项并按需驱动排空 对应原版 addAndRun
    //执行中产生的新项进延迟列表 只有非重入的顶层调用才启动排空
    //链长超限直接丢弃该项 只在首次越界打一条日志
    private void AddAndRun(BlockPos pos, INeighborUpdate update)
    {
        var runningAlready = _count > 0;
        var tooManyUpdates = _maxChainedNeighborUpdates >= 0 && _count >= _maxChainedNeighborUpdates;
        _count++;
        if (!tooManyUpdates)
        {
            if (runningAlready) _addedThisLayer.Add(update);
            else _stack.Push(update);
        }
        else if (_count - 1 == _maxChainedNeighborUpdates)
        {
            Log.Error($"Neighbor update chain overflow at {pos}, dropping the rest");
        }

        if (!runningAlready) RunUpdates();
    }

    //RunUpdates 排空整条队列 对应原版 runUpdates
    //同一队列项会被反复 RunNext 直到它做完或产生新更新 新更新打断当前项并抢先继续
    //原版反编译把 try/finally 嵌进了 while 内层 按语义应是 try 包住整个循环
    //照反编译写会让队列每次只执行一项 邻居更新链在第一跳就断掉
    private void RunUpdates()
    {
        try
        {
            while (_stack.Count > 0 || _addedThisLayer.Count > 0)
            {
                //新增项逆序压栈 保证同批新项仍按添加顺序执行
                for (var i = _addedThisLayer.Count - 1; i >= 0; i--)
                    _stack.Push(_addedThisLayer[i]);
                _addedThisLayer.Clear();
                var next = _stack.Peek();
                while (true)
                {
                    if (_addedThisLayer.Count != 0) break;
                    if (!next.RunNext(_level))
                    {
                        _stack.Pop();
                        break;
                    }
                }
            }
        }
        finally
        {
            //正常结束或异常展开都要复位 免得留下脏状态让后续调用连锁出错
            _stack.Clear();
            _addedThisLayer.Clear();
            _count = 0;
        }
    }

    //SimpleNeighborUpdate 执行时重读位置的邻居更新项
    private sealed class SimpleNeighborUpdate : INeighborUpdate
    {
        private readonly BlockPos _pos;
        private readonly NetCraft.Registry.Block _changedBlock;

        public SimpleNeighborUpdate(BlockPos pos, NetCraft.Registry.Block changedBlock)
        {
            _pos = pos;
            _changedBlock = changedBlock;
        }

        public bool RunNext(ServerLevel level)
        {
            var state = level.GetBlockState(_pos);
            if (state is not null)
                NeighborUpdater.ExecuteUpdate(level, state.Value, _pos, _changedBlock, false);
            return false;
        }
    }

    //FullNeighborUpdate 用入队时快照的邻居更新项 执行时不再重读
    private sealed class FullNeighborUpdate : INeighborUpdate
    {
        private readonly BlockState _state;
        private readonly BlockPos _pos;
        private readonly NetCraft.Registry.Block _changedBlock;
        private readonly bool _movedByPiston;

        public FullNeighborUpdate(BlockState state, BlockPos pos, NetCraft.Registry.Block changedBlock,
            bool movedByPiston)
        {
            _state = state;
            _pos = pos;
            _changedBlock = changedBlock;
            _movedByPiston = movedByPiston;
        }

        public bool RunNext(ServerLevel level)
        {
            NeighborUpdater.ExecuteUpdate(level, _state, _pos, _changedBlock, _movedByPiston);
            return false;
        }
    }

    //MultiNeighborUpdate 一次向六方向发更新 每步只走一个方向
    //其它更新可以插进它的方向循环中间 红石更新顺序靠这个机制成立
    private sealed class MultiNeighborUpdate : INeighborUpdate
    {
        private readonly BlockPos _sourcePos;
        private readonly NetCraft.Registry.Block _sourceBlock;
        private readonly Direction? _skipDirection;
        private int _idx;

        public MultiNeighborUpdate(BlockPos sourcePos, NetCraft.Registry.Block sourceBlock, Direction? skipDirection)
        {
            _sourcePos = sourcePos;
            _sourceBlock = sourceBlock;
            _skipDirection = skipDirection;
            if (IsSkipped(_idx)) _idx++;
        }

        public bool RunNext(ServerLevel level)
        {
            var order = BlockUpdateFlags.NeighbourUpdateOrder;
            var direction = order[_idx];
            _idx++;
            var neighbourPos = _sourcePos.Offset(direction);
            var state = level.GetBlockState(neighbourPos);
            if (state is not null)
                NeighborUpdater.ExecuteUpdate(level, state.Value, neighbourPos, _sourceBlock, false);
            if (_idx < order.Length && IsSkipped(_idx)) _idx++;
            return _idx < order.Length;
        }

        private bool IsSkipped(int index)
            => _skipDirection is { } skip && BlockUpdateFlags.NeighbourUpdateOrder[index] == skip;
    }

    //ShapeUpdateTask 携带邻接状态快照的形状更新项
    //名字带 Task 后缀是为了避开同名公开方法 shapeUpdate
    private sealed class ShapeUpdateTask : INeighborUpdate
    {
        private readonly Direction _direction;
        private readonly BlockState _neighbourState;
        private readonly BlockPos _pos;
        private readonly BlockPos _neighbourPos;
        private readonly int _updateFlags;
        private readonly int _updateLimit;

        public ShapeUpdateTask(Direction direction, BlockState neighbourState, BlockPos pos, BlockPos neighbourPos,
            int updateFlags, int updateLimit)
        {
            _direction = direction;
            _neighbourState = neighbourState;
            _pos = pos;
            _neighbourPos = neighbourPos;
            _updateFlags = updateFlags;
            _updateLimit = updateLimit;
        }

        public bool RunNext(ServerLevel level)
        {
            NeighborUpdater.ExecuteShapeUpdate(level, _direction, _pos, _neighbourPos, _neighbourState,
                _updateFlags, _updateLimit);
            return false;
        }
    }
}
