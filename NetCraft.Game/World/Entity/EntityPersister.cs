using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//EntityPersister entity save codec, injected into the Storage layer EntityStorage
//Maps to vanilla Entity.save and EntityType.loadEntityRecursive
//The type is resolved from the save id field via the ENTITY_TYPE registry; unknown or non-instantiable types are dropped entirely
public static class EntityPersister
{
    private const string IdTag = "id";

    //Save writes the entity as nbt, maps to vanilla Entity.save
    public static CompoundTag Save(NetCraft.Registry.Entity entity)
    {
        var tag = new CompoundTag();
        entity.Save(tag);
        return tag;
    }

    //Load restores an entity from nbt; returns null when the id is missing, not in the table or the type has no factory, and the caller skips that entry
    //The return type uses a fully qualified name since the current namespace contains an Entity segment that would shadow the same-named type
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

        //ENTITY_TYPE is a DefaultedRegistry, unknown ids fall back to the registry default, so existence must be checked first
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
