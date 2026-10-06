namespace NetCraft.Registry;

//EntityType abstract base class, maps to vanilla net.minecraft.world.entity.EntityType
//T is the concrete Entity subclass; vanilla holds EntityFactory/Codec/MobCategory
//Simplified to an abstract class here holding Id/RawId and the entity factory
public abstract class EntityType<T> where T : class
{
    //Id the entity type's registry name, must be implemented by subclasses
    public abstract Identifier Id { get; }

    //RawId network index; the AddEntity packet sends it and it must match the client's ENTITY_TYPE registry index
    public abstract int RawId { get; }

    //TrackingRangeChunks the view distance in chunks the entity is tracked over, maps to vanilla clientTrackingRange, default 10
    public virtual int TrackingRangeChunks => 10;

    //Width hitbox width (in blocks), maps to vanilla EntityType.Builder.sized width, defaulting to the player size
    public virtual float Width => 0.6f;

    //Height hitbox height (in blocks), maps to vanilla EntityType.Builder.sized height
    public virtual float Height => 1.8f;

    //Factory entity factory, maps to vanilla EntityType.EntityFactory
    //level uses object as a placeholder, consistent with Entity.Level
    //Types without a factory cannot be instantiated (e.g. player entities go through playerdata and have no entity storage)
    public Func<EntityType<T>, object?, Entity>? Factory { get; init; }

    //Create creates an entity instance from the factory and backfills the level onto it, maps to vanilla EntityType.create
    //Returns null when there is no factory or the factory returns null
    public Entity? Create(object? level)
    {
        var entity = Factory?.Invoke(this, level);
        if (entity is not null) entity.Level = level;
        return entity;
    }
}
