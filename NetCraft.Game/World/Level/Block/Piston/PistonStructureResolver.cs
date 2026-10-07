using NetCraft.Primitives;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
//Direction exists in both Primitives and Registry.Enums; the one used for blocks is taken here
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonStructureResolver structural resolution of one push, maps to the vanilla class of the same name
//Collects cell by cell from in front of the piston along the push direction: pushable blocks go into toPush and breakable ones into toDestroy
//At most 12 blocks, beyond that it counts as immovable; slime and honey blocks also bring out side branches
internal sealed class PistonStructureResolver
{
    //MaxPushDepth maximum block count of one push, maps to vanilla MAX_PUSH_DEPTH
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
            //On retract the move starts at the segment two cells behind the piston head
            _pushDirection = direction.Opposite;
            _startPos = pistonPos.Relative(direction, 2);
        }
    }

    public IReadOnlyList<BlockPos> ToPush => _toPush;

    public IReadOnlyList<BlockPos> ToDestroy => _toDestroy;

    public Direction PushDirection => _pushDirection;

    //Resolve resolves the complete push structure and returns whether it can move
    public bool Resolve()
    {
        _toPush.Clear();
        _toDestroy.Clear();
        var nextState = StateAt(_startPos);
        if (!Blocks.PistonBaseBlock.IsPushable(nextState, _level, _startPos, _pushDirection, false, _pistonDirection))
        {
            //When extending it destroys breakable blocks on the spot
            if (_extending && PistonPushReactions.Of(nextState) == PushReaction.destroy)
            {
                _toDestroy.Add(_startPos);
                return true;
            }
            return false;
        }
        if (!AddBlockLine(_startPos, _pushDirection)) return false;
        //Sticky blocks collect their laterally stuck branches too
        for (var i = 0; i < _toPush.Count; i++)
        {
            var pos = _toPush[i];
            if (!IsSticky(StateAt(pos)) || AddBranchingBlocks(pos)) continue;
            return false;
        }
        return true;
    }

    //IsSticky whether the block can stick to neighbors, maps to vanilla isSticky
    private static bool IsSticky(BlockState state)
        => state.Owner.Id.Path is "slime_block" or "honey_block";

    //CanStickToEachOther whether two blocks stick; honey and slime do not stick to each other, maps to vanilla canStickToEachOther
    private static bool CanStickToEachOther(BlockState first, BlockState second)
    {
        if (first.Owner.Id.Path == "honey_block" && second.Owner.Id.Path == "slime_block") return false;
        if (first.Owner.Id.Path == "slime_block" && second.Owner.Id.Path == "honey_block") return false;
        return IsSticky(first) || IsSticky(second);
    }

    //AddBlockLine collects blocks along a line in direction, maps to vanilla addBlockLine
    private bool AddBlockLine(BlockPos start, Direction direction)
    {
        var nextState = StateAt(start);
        if (nextState.Owner.IsAir) return true;
        if (!Blocks.PistonBaseBlock.IsPushable(nextState, _level, start, _pushDirection, false, direction)) return true;
        if (start.Equals(_pistonPos)) return true;
        if (_toPush.Contains(start)) return true;
        var blockCount = 1;
        if (blockCount + _toPush.Count > MaxPushDepth) return false;
        //Slime blocks strung together drag out a long line behind, so its length is counted first
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
                //Running into an already collected line means the two lines merge into one
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

    //ReorderListAtCollision moves the later line behind the junction when two lines meet, maps to vanilla reorderListAtCollision
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

    //AddBranchingBlocks collects laterally stuck branches, maps to vanilla addBranchingBlocks
    private bool AddBranchingBlocks(BlockPos fromPos)
    {
        var fromState = StateAt(fromPos);
        foreach (var direction in Direction.Values)
        {
            //The two cells whose axis matches this line are already on it, so they are skipped
            if (direction.GetAxis() == _pushDirection.GetAxis()) continue;
            var neighbourPos = fromPos.Offset(direction);
            var neighbourState = StateAt(neighbourPos);
            if (!CanStickToEachOther(neighbourState, fromState) || AddBlockLine(neighbourPos, direction)) continue;
            return false;
        }
        return true;
    }

    //StateAt reads the block state and treats an unloaded chunk as air, maps to the vanilla getBlockState fallback
    private BlockState StateAt(BlockPos pos)
        => _level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;
}
