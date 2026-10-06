using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Updates;

//CollectingNeighborUpdater, the collecting updater, maps to vanilla CollectingNeighborUpdater
//One stack + a deferred list + a reentrancy count; both channels share one queue and interleave by the same rules
//Draining happens inside the call stack, not per tick, and there is no per-tick budget
public sealed class CollectingNeighborUpdater : INeighborUpdater
{
    private readonly ServerLevel _level;
    //A negative value means the chain length is unlimited
    private readonly int _maxChainedNeighborUpdates;
    //_stack, pending items, LIFO
    private readonly Stack<INeighborUpdate> _stack = new();
    //_addedThisLayer, updates created while the current item runs, deferred until it finishes before being pushed
    private readonly List<INeighborUpdate> _addedThisLayer = new();
    //_count, reentrancy count and chain length counter
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

    //AddAndRun enqueues an item and drives draining as needed, maps to vanilla addAndRun
    //New items created during execution go to the deferred list; only a non-reentrant top-level call starts draining
    //When the chain length is exceeded the item is dropped, with a log only on the first overflow
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

    //RunUpdates drains the whole queue, maps to vanilla runUpdates
    //The same queue item is RunNext repeatedly until it finishes or produces new updates; new updates interrupt the current item and run first
    //The vanilla decompile nests try/finally inside the while; semantically try should wrap the whole loop
    //Writing it per the decompile would execute only one item at a time, breaking the neighbor update chain at the first hop
    private void RunUpdates()
    {
        try
        {
            while (_stack.Count > 0 || _addedThisLayer.Count > 0)
            {
                //New items are pushed in reverse so the same batch still executes in addition order
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
            //Reset on normal completion or exception unwind, so no dirty state causes chained failures later
            _stack.Clear();
            _addedThisLayer.Clear();
            _count = 0;
        }
    }

    //SimpleNeighborUpdate, a neighbor update item that re-reads the pos on execution
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

    //FullNeighborUpdate, a neighbor update item using the state snapshot taken at enqueue; not re-read on execution
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

    //MultiNeighborUpdate sends updates in all six directions, one direction per step
    //Other updates can interleave into its direction loop; the redstone update order relies on this
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

    //ShapeUpdateTask, a shape update item carrying a neighbor state snapshot
    //The Task suffix avoids clashing with the same-named public shapeUpdate method
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
