using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level.Block;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Commands.Data;

//BlockDataAccessor 方块实体数据访问对应原版 net.minecraft.server.commands.data.BlockDataAccessor
//取的是方块实体自身的 NBT 目标位置没有方块实体时命令报错
public sealed class BlockDataAccessor : IDataAccessor
{
    //ErrorNotBlockEntity 目标位置没有方块实体 对应原版 ERROR_NOT_A_BLOCK_ENTITY
    public static readonly SimpleCommandExceptionType ErrorNotBlockEntity =
        new(new LiteralMessage("目标位置没有方块实体"));

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

    //SetData 整份读回方块实体 对应原版 setData 的 loadWithComponents + sendBlockUpdated
    //原版写完标记脏并广播 3 号更新 这里广播最新数据并通知邻居
    //比较器这类输出强度存在方块实体里 不通知邻居就不会重新求值
    public void SetData(CompoundTag tag)
    {
        _entity.LoadCustomOnly(tag);
        _players.BroadcastAll(_entity.GetUpdatePacket());
        if (_level.GetBlockState(_pos) is { } state) _level.UpdateNeighborsAt(_pos, state.Owner);
    }

    public CompoundTag GetData() => _entity.SaveWithFullMetadata();

    public string ModifiedSuccess => $"已修改位置 {Format()} 的方块实体数据";

    public string PrintSuccess(Tag data)
        => $"位置 {Format()} 的方块实体数据:\n{NbtUtils.PrettyPrint(data, false)}";

    public string PrintSuccess(NbtPath path, double scale, int value)
        => $"位置 {Format()} 的 {path} 乘以 {scale:0.00} 后为 {value}";

    private string Format() => $"{_pos.X} {_pos.Y} {_pos.Z}";
}
