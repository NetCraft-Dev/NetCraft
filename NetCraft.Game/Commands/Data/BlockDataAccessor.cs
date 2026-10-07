using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level.Block;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Commands.Data;

//BlockDataAccessor block entity data access, maps to vanilla net.minecraft.server.commands.data.BlockDataAccessor
//Takes the block entity's own NBT; the command errors when the target position has no block entity
public sealed class BlockDataAccessor : IDataAccessor
{
    //ErrorNotBlockEntity no block entity at the target position, maps to vanilla ERROR_NOT_A_BLOCK_ENTITY
    public static readonly SimpleCommandExceptionType ErrorNotBlockEntity =
        new(new LiteralMessage("no block entity at the target position"));

    private readonly PersistentServerLevel _level;
    private readonly PlayerList _players;
    private readonly BlockEntity _entity;
    private readonly BlockPos _pos;

    public BlockDataAccessor(PersistentServerLevel level, PlayerList players, BlockEntity entity, BlockPos pos)
    {
        _level = level;
        _players = players;
        _entity = entity;
        _pos = pos;
    }

    //SetData reads the block entity back whole, maps to vanilla setData's loadWithComponents + sendBlockUpdated
    //Vanilla marks dirty and broadcasts update 3 after writing; this broadcasts the latest data and notifies neighbors
    //Output strength such as comparators lives in the block entity; without notifying neighbors it is not re-evaluated
    public void SetData(CompoundTag tag)
    {
        _entity.LoadCustomOnly(tag);
        _players.BroadcastAll(_entity.GetUpdatePacket());
        if (_level.GetBlockState(_pos) is { } state) _level.UpdateNeighborsAt(_pos, state.Owner);
    }

    public CompoundTag GetData() => _entity.SaveWithFullMetadata();

    public string ModifiedSuccess => $"modified block entity data at {Format()}";

    public string PrintSuccess(Tag data)
        => $"block entity data at {Format()}:\n{NbtUtils.PrettyPrint(data, false)}";

    public string PrintSuccess(NbtPath path, double scale, int value)
        => $"the {path} at {Format()} times {scale:0.00} is {value}";

    private string Format() => $"{_pos.X} {_pos.Y} {_pos.Z}";
}
