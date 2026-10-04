using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Data;

//EntityDataAccessor 实体数据访问对应原版 net.minecraft.server.commands.data.EntityDataAccessor
//玩家只允许读不允许写 玩家状态不是整份 NBT 重载能安全覆盖的
public sealed class EntityDataAccessor : IDataAccessor
{
    //ErrorNoPlayers 禁止把 NBT 写回玩家 对应原版 ERROR_NO_PLAYERS
    public static readonly SimpleCommandExceptionType ErrorNoPlayers =
        new(new LiteralMessage("该命令不能修改玩家的数据"));

    private readonly CommandTarget _target;

    public EntityDataAccessor(CommandTarget target) => _target = target;

    public void SetData(CompoundTag tag)
    {
        if (_target.Player is not null) throw ErrorNoPlayers.Create();
        var entity = _target.WorldEntity!;
        //原版写完把 UUID 再还原 存档里的唯一标识不该被 /data 改掉
        var uuid = entity.Uuid;
        entity.Load(tag);
        entity.Uuid = uuid;
    }

    //GetData 玩家走玩家存档序列化 关卡实体走 Entity.saveWithoutId 对应原版 getEntityTagToCompare
    public CompoundTag GetData()
    {
        if (_target.Player is { } player) return PlayerDataStorage.CreateTag(player);
        var tag = new CompoundTag();
        _target.WorldEntity!.SaveWithoutId(tag);
        return tag;
    }

    public string ModifiedSuccess => $"已修改实体 {_target.Name} 的数据";

    public string PrintSuccess(Tag data)
        => $"实体 {_target.Name} 的数据:\n{NbtUtils.PrettyPrint(data, false)}";

    public string PrintSuccess(NbtPath path, double scale, int value)
        => $"实体 {_target.Name} 的 {path} 乘以 {scale:0.00} 后为 {value}";
}
