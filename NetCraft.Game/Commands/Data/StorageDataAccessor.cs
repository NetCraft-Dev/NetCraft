using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands.Data;

//StorageDataAccessor 命令存储数据访问对应原版 net.minecraft.server.commands.data.StorageDataAccessor
//目标是命令自己用的键值存储 与方块/实体无关 常用来在命令之间传值
public sealed class StorageDataAccessor : IDataAccessor
{
    private readonly CommandStorage _storage;
    private readonly Identifier _id;

    public StorageDataAccessor(CommandStorage storage, Identifier id)
    {
        _storage = storage;
        _id = id;
    }

    public void SetData(CompoundTag tag) => _storage.Set(_id, tag);

    public CompoundTag GetData() => _storage.Get(_id);

    public string ModifiedSuccess => $"已修改存储 {_id} 的数据";

    public string PrintSuccess(Tag data)
        => $"存储 {_id} 的数据:\n{NbtUtils.PrettyPrint(data, false)}";

    public string PrintSuccess(NbtPath path, double scale, int value)
        => $"存储 {_id} 的 {path} 乘以 {scale:0.00} 后为 {value}";
}
