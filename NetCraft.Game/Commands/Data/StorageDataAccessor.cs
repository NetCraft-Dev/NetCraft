using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands.Data;

//StorageDataAccessor command storage data access, maps to vanilla net.minecraft.server.commands.data.StorageDataAccessor
//The target is the key-value storage commands use themselves, unrelated to blocks/entities, often used to pass values between commands
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

    public string ModifiedSuccess => $"modified data of storage {_id}";

    public string PrintSuccess(Tag data)
        => $"data of storage {_id}:\n{NbtUtils.PrettyPrint(data, false)}";

    public string PrintSuccess(NbtPath path, double scale, int value)
        => $"the {path} of storage {_id} times {scale:0.00} is {value}";
}
