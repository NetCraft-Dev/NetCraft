namespace NetCraft.Registry;

//EntityType 抽象基类对应原版 net.minecraft.world.entity.EntityType
//T 是具体 Entity 子类原版持有 EntityFactory/Codec/MobCategory
//此处简化为抽象类持有 Id/RawId 与实体工厂
public abstract class EntityType<T> where T : class
{
    //Id 实体类型的注册名子类必须实现
    public abstract Identifier Id { get; }

    //RawId 网络序号 AddEntity 包按该序号下发 必须与客户端 ENTITY_TYPE 注册表序号一致
    public abstract int RawId { get; }

    //TrackingRangeChunks 实体被追踪的视距区块数 对应原版 clientTrackingRange 默认 10
    public virtual int TrackingRangeChunks => 10;

    //Width 碰撞盒宽度(格) 对应原版 EntityType.Builder.sized 的 width 默认取玩家尺寸
    public virtual float Width => 0.6f;

    //Height 碰撞盒高度(格) 对应原版 EntityType.Builder.sized 的 height
    public virtual float Height => 1.8f;

    //Factory 实体工厂 对应原版 EntityType.EntityFactory
    //level 用 object 占位与 Entity.Level 保持一致
    //未设工厂的类型不可实例化(如玩家实体走 playerdata 不落实体存储)
    public Func<EntityType<T>, object?, Entity>? Factory { get; init; }

    //Create 由工厂创建实体实例并把关卡回填到实体上 对应原版 EntityType.create
    //无工厂或工厂返回 null 时返回 null
    public Entity? Create(object? level)
    {
        var entity = Factory?.Invoke(this, level);
        if (entity is not null) entity.Level = level;
        return entity;
    }
}
