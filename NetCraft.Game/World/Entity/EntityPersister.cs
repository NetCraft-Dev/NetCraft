using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//EntityPersister 实体存档编解码 注入给 Storage 层 EntityStorage 使用
//对应原版 Entity.save 与 EntityType.loadEntityRecursive
//类型从存档 id 字段按 ENTITY_TYPE 注册表解析 未知或不可实例化的类型整条丢弃
public static class EntityPersister
{
    private const string IdTag = "id";

    //Save 把实体写成 nbt 对应原版 Entity.save
    public static CompoundTag Save(NetCraft.Registry.Entity entity)
    {
        var tag = new CompoundTag();
        entity.Save(tag);
        return tag;
    }

    //Load 从 nbt 还原实体 缺 id/表里没有/类型无工厂时返回 null 由调用方跳过该条
    //返回类型用全限定名 当前命名空间含 Entity 段会遮蔽同名类型
    public static NetCraft.Registry.Entity? Load(CompoundTag tag, RegistryAccess access)
    {
        var idText = tag.GetStringValue(IdTag);
        if (string.IsNullOrEmpty(idText))
        {
            Log.Warning("Entity save is missing the id field, skipped");
            return null;
        }

        Identifier identifier;
        try
        {
            identifier = Identifier.Parse(idText);
        }
        catch (Exception e)
        {
            Log.Warning($"Entity save has an invalid id {idText}: {e.Message}");
            return null;
        }

        //ENTITY_TYPE 是 DefaultedRegistry 未知 id 取值会兜底成注册表默认项 必须先判存在
        if (!BuiltInRegistries.ENTITY_TYPE.ContainsKey(identifier))
        {
            Log.Warning($"Entity type not registered {idText}, skipped");
            return null;
        }

        var type = BuiltInRegistries.ENTITY_TYPE.GetValue(identifier);
        var entity = type?.Create(null);
        if (entity is null)
        {
            Log.Warning($"Entity type cannot be instantiated {idText}, skipped");
            return null;
        }

        entity.Load(tag);
        return entity;
    }
}
