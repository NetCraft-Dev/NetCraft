using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Data;

//EntityDataAccessor entity data access, maps to vanilla net.minecraft.server.commands.data.EntityDataAccessor
//Players may be read but not written; player state is not something a whole-NBT reload can safely overwrite
public sealed class EntityDataAccessor : IDataAccessor
{
    //ErrorNoPlayers writing NBT back to a player is forbidden, maps to vanilla ERROR_NO_PLAYERS
    public static readonly SimpleCommandExceptionType ErrorNoPlayers =
        new(new LiteralMessage("this command cannot modify player data"));

    private readonly CommandTarget _target;

    public EntityDataAccessor(CommandTarget target) => _target = target;

    public void SetData(CompoundTag tag)
    {
        if (_target.Player is not null) throw ErrorNoPlayers.Create();
        var entity = _target.WorldEntity!;
        //Vanilla restores the UUID after writing; the save's unique id should not be changed by /data
        var uuid = entity.Uuid;
        entity.Load(tag);
        entity.Uuid = uuid;
    }

    //GetData: players go through player save serialization, level entities through Entity.saveWithoutId, maps to vanilla getEntityTagToCompare
    public CompoundTag GetData()
    {
        if (_target.Player is { } player) return PlayerDataStorage.CreateTag(player);
        var tag = new CompoundTag();
        _target.WorldEntity!.SaveWithoutId(tag);
        return tag;
    }

    public string ModifiedSuccess => $"modified data of entity {_target.Name}";

    public string PrintSuccess(Tag data)
        => $"data of entity {_target.Name}:\n{NbtUtils.PrettyPrint(data, false)}";

    public string PrintSuccess(NbtPath path, double scale, int value)
        => $"the {path} of entity {_target.Name} times {scale:0.00} is {value}";
}
