using NetCraft.Primitives;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
//方向同时存在于 Primitives 与 Registry.Enums 这里取方块用的那套
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonStructureResolver 一次推动的结构解析 对应原版同名类
//从活塞前方开始沿推动方向逐格收集 要推走的进 toPush 要破坏的进 toDestroy
//最多 12 格 超过就判定推不动 粘液块与蜂蜜块还会带出侧向分支
internal sealed class PistonStructureResolver
{
    //MaxPushDepth 一次推动的方块数上限 对应原版 MAX_PUSH_DEPTH
    public const int MaxPushDepth = 12;

    private readonly ServerLevel _level;
    private readonly BlockPos _pistonPos;
    private readonly bool _extending;
    private readonly BlockPos _startPos;
    private readonly Direction _pushDirection;
    private readonly Direction _pistonDirection;
    private readonly List<BlockPos> _toPush = new();
    private readonly List<BlockPos> _toDestroy = new();

    public PistonStructureResolver(ServerLevel level, BlockPos pistonPos, Direction direction, bool extending)
    {
        _level = level;
        _pistonPos = pistonPos;
        _pistonDirection = direction;
        _extending = extending;
        if (extending)
        {
            _pushDirection = direction;
            _startPos = pistonPos.Relative(direction, 1);
        }
        else
        {
            //收回时从活塞头后面两格那截开始搬
            _pushDirection = direction.Opposite;
            _startPos = pistonPos.Relative(direction, 2);
        }
    }

    public IReadOnlyList<BlockPos> ToPush => _toPush;

    public IReadOnlyList<BlockPos> ToDestroy => _toDestroy;

    public Direction PushDirection => _pushDirection;

    //Resolve 解析出完整推动结构 返回是否推得动
    public bool Resolve()
    {
        _toPush.Clear();
        _toDestroy.Clear();
        var nextState = StateAt(_startPos);
        if (!Blocks.PistonBaseBlock.IsPushable(nextState, _level, _startPos, _pushDirection, false, _pistonDirection))
        {
            //伸出时遇到可破坏方块就地毁掉
            if (_extending && PistonPushReactions.Of(nextState) == PushReaction.destroy)
            {
                _toDestroy.Add(_startPos);
                return true;
            }
            return false;
        }
        if (!AddBlockLine(_startPos, _pushDirection)) return false;
        //粘性方块要把侧向粘着的分支一起收集
        for (var i = 0; i < _toPush.Count; i++)
        {
            var pos = _toPush[i];
            if (!IsSticky(StateAt(pos)) || AddBranchingBlocks(pos)) continue;
            return false;
        }
        return true;
    }

    //IsSticky 能粘住邻居的方块 对应原版 isSticky
    private static bool IsSticky(BlockState state)
        => state.Owner.Id.Path is "slime_block" or "honey_block";

    //CanStickToEachOther 两块之间粘不粘 蜂蜜块与粘液块互不粘 对应原版 canStickToEachOther
    private static bool CanStickToEachOther(BlockState first, BlockState second)
    {
        if (first.Owner.Id.Path == "honey_block" && second.Owner.Id.Path == "slime_block") return false;
        if (first.Owner.Id.Path == "slime_block" && second.Owner.Id.Path == "honey_block") return false;
        return IsSticky(first) || IsSticky(second);
    }

    //AddBlockLine 沿 direction 收一条线上的方块 对应原版 addBlockLine
    private bool AddBlockLine(BlockPos start, Direction direction)
    {
        var nextState = StateAt(start);
        if (nextState.Owner.IsAir) return true;
        if (!Blocks.PistonBaseBlock.IsPushable(nextState, _level, start, _pushDirection, false, direction)) return true;
        if (start.Equals(_pistonPos)) return true;
        if (_toPush.Contains(start)) return true;
        var blockCount = 1;
        if (blockCount + _toPush.Count > MaxPushDepth) return false;
        //粘液块串在一起会拖出身后一长条 先数清楚有多长
        while (IsSticky(nextState))
        {
            var pos = start.Relative(_pushDirection.Opposite, blockCount);
            var previousState = nextState;
            nextState = StateAt(pos);
            if (nextState.Owner.IsAir
                || !CanStickToEachOther(previousState, nextState)
                || !Blocks.PistonBaseBlock.IsPushable(nextState, _level, pos, _pushDirection, false, _pushDirection.Opposite)
                || pos.Equals(_pistonPos))
                break;
            if (++blockCount + _toPush.Count <= MaxPushDepth) continue;
            return false;
        }
        var blocksAdded = 0;
        for (var i = blockCount - 1; i >= 0; i--)
        {
            _toPush.Add(start.Relative(_pushDirection.Opposite, i));
            blocksAdded++;
        }
        var distance = 1;
        while (true)
        {
            var pos = start.Relative(_pushDirection, distance);
            var collisionPos = _toPush.IndexOf(pos);
            if (collisionPos > -1)
            {
                //撞上已经收进来的那一条 两条线要并成一条
                ReorderListAtCollision(blocksAdded, collisionPos);
                for (var j = 0; j <= collisionPos + blocksAdded; j++)
                {
                    var blockPos = _toPush[j];
                    if (!IsSticky(StateAt(blockPos)) || AddBranchingBlocks(blockPos)) continue;
                    return false;
                }
                return true;
            }
            nextState = StateAt(pos);
            if (nextState.Owner.IsAir) return true;
            if (!Blocks.PistonBaseBlock.IsPushable(nextState, _level, pos, _pushDirection, true, _pushDirection)
                || pos.Equals(_pistonPos))
                return false;
            if (PistonPushReactions.Of(nextState) == PushReaction.destroy)
            {
                _toDestroy.Add(pos);
                return true;
            }
            if (_toPush.Count >= MaxPushDepth) return false;
            _toPush.Add(pos);
            blocksAdded++;
            distance++;
        }
    }

    //ReorderListAtCollision 两条线接上时把后加入的那条提到接点后面 对应原版 reorderListAtCollision
    private void ReorderListAtCollision(int blocksAdded, int collisionPos)
    {
        var head = _toPush.GetRange(0, collisionPos);
        var lastLineAdded = _toPush.GetRange(_toPush.Count - blocksAdded, blocksAdded);
        var collisionToLine = _toPush.GetRange(collisionPos, _toPush.Count - blocksAdded - collisionPos);
        _toPush.Clear();
        _toPush.AddRange(head);
        _toPush.AddRange(lastLineAdded);
        _toPush.AddRange(collisionToLine);
    }

    //AddBranchingBlocks 收侧向粘着的分支 对应原版 addBranchingBlocks
    private bool AddBranchingBlocks(BlockPos fromPos)
    {
        var fromState = StateAt(fromPos);
        foreach (var direction in Direction.Values)
        {
            //轴向与本条线相同的那两格已经在线上 不用再看
            if (direction.GetAxis() == _pushDirection.GetAxis()) continue;
            var neighbourPos = fromPos.Offset(direction);
            var neighbourState = StateAt(neighbourPos);
            if (!CanStickToEachOther(neighbourState, fromState) || AddBlockLine(neighbourPos, direction)) continue;
            return false;
        }
        return true;
    }

    //StateAt 读方块状态 区块没加载按空气处理 对应原版 getBlockState 兜底
    private BlockState StateAt(BlockPos pos)
        => _level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;
}
