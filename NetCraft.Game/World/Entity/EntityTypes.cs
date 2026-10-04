using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//EntityTypes 内置实体类型常量对应原版 net.minecraft.world.entity.EntityTypes
//注册到 BuiltInRegistries.ENTITY_TYPE 注册表
//简化版只含几个示例实体(猪/牛/鸡/僵尸/玩家)验证注册框架 原版有 158 个类型 NC 按需扩展
//RawId 取自原版 EntityTypes.java 的注册顺序序号 客户端按序号解析 AddEntity 包故不能自行编号
//注意 BuiltInRegistries.ENTITY_TYPE 是 EntityType<object> 弱类型注册表
//用 object 类型参数承载不同具体 Entity 子类对齐原版类型擦除方案
public static class EntityTypes
{
    //PigType 猪类型
    public sealed class PigType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("pig");
        public override int RawId => 100;
    }

    //CowType 牛类型
    public sealed class CowType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("cow");
        public override int RawId => 30;
    }

    //ChickenType 鸡类型
    public sealed class ChickenType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("chicken");
        public override int RawId => 26;
    }

    //ZombieType 僵尸类型
    public sealed class ZombieType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("zombie");
        public override int RawId => 151;
    }

    //PlayerType 玩家类型 玩家追踪与 AddEntity 广播用
    public sealed class PlayerType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("player");
        public override int RawId => 156;
        //玩家视距最大 对应原版 clientTrackingRange(32)
        public override int TrackingRangeChunks => 32;
    }

    //ItemType 掉落物类型 对应原版 EntityTypes.ITEM 的 clientTrackingRange(6)
    public sealed class ItemType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("item");
        public override int RawId => 71;
        //掉落物追踪视距比生物近 对应原版 clientTrackingRange(6)
        public override int TrackingRangeChunks => 6;
        //掉落物碰撞盒 0.25 见方 对应原版 sized(0.25f, 0.25f)
        public override float Width => 0.25f;
        public override float Height => 0.25f;
    }

    //ArrowType 箭类型 对应原版 EntityTypes.ARROW
    public sealed class ArrowType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("arrow");
        public override int RawId => 6;
        public override int TrackingRangeChunks => 4;
        public override float Width => 0.5f;
        public override float Height => 0.5f;
    }

    //EggType 鸡蛋类型 对应原版 EntityTypes.EGG
    public sealed class EggType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("egg");
        public override int RawId => 39;
        public override int TrackingRangeChunks => 4;
        public override float Width => 0.25f;
        public override float Height => 0.25f;
    }

    //EnderPearlType 末影珍珠类型 对应原版 EntityTypes.ENDER_PEARL
    public sealed class EnderPearlType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("ender_pearl");
        public override int RawId => 44;
        public override int TrackingRangeChunks => 4;
        public override float Width => 0.25f;
        public override float Height => 0.25f;
    }

    //SmallFireballType 小火球类型 火焰弹投出去就是它 对应原版 EntityTypes.SMALL_FIREBALL
    public sealed class SmallFireballType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("small_fireball");
        public override int RawId => 118;
        public override int TrackingRangeChunks => 4;
        public override float Width => 0.3125f;
        public override float Height => 0.3125f;
    }

    //SnowballType 雪球类型 对应原版 EntityTypes.SNOWBALL
    public sealed class SnowballType : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("snowball");
        public override int RawId => 120;
        public override int TrackingRangeChunks => 4;
        public override float Width => 0.25f;
        public override float Height => 0.25f;
    }

    public static readonly PigType PIG = new() { Factory = (type, level) => new Mob(type) };
    public static readonly CowType COW = new() { Factory = (type, level) => new Mob(type) };
    public static readonly ChickenType CHICKEN = new() { Factory = (type, level) => new Mob(type) };
    public static readonly ZombieType ZOMBIE = new() { Factory = (type, level) => new Mob(type) };
    public static readonly ItemType ITEM = new() { Factory = (type, level) => new ItemEntity(type) };

    //投射物五类 全部只有发射时的落点差异 行为由各自实体类决定
    public static readonly ArrowType ARROW = new() { Factory = (type, level) => new Arrow(type) };
    public static readonly EggType EGG = new() { Factory = (type, level) => new ThrownEgg(type) };
    public static readonly EnderPearlType ENDER_PEARL = new() { Factory = (type, level) => new ThrownEnderpearl(type) };
    public static readonly SnowballType SNOWBALL = new() { Factory = (type, level) => new Snowball(type) };
    public static readonly SmallFireballType SMALL_FIREBALL = new() { Factory = (type, level) => new SmallFireball(type) };

    //玩家实体走 playerdata 不落实体存储 故不给工厂 从实体存档还原时跳过
    public static readonly PlayerType PLAYER = new();

    //_byRawId 网络序号到类型的反查表 供 AddEntity 解码解析实体类型
    private static readonly Dictionary<int, EntityType<object>> ByRawId = new();

    //Bootstrap 注册所有内置实体类型到 BuiltInRegistries.ENTITY_TYPE
    //由 Game 层 Bootstrap 在 BuiltInRegistries.BootStrap 后调用
    public static void Bootstrap()
    {
        Register(PIG);
        Register(COW);
        Register(CHICKEN);
        Register(ZOMBIE);
        Register(ITEM);
        Register(ARROW);
        Register(EGG);
        Register(ENDER_PEARL);
        Register(SNOWBALL);
        Register(SMALL_FIREBALL);
        Register(PLAYER);
    }

    //ById 按网络序号解析实体类型 未注册返回 null
    public static EntityType<object>? ById(int rawId)
        => ByRawId.TryGetValue(rawId, out var type) ? type : null;

    //UnknownType 未注册网络序号的占位类型 客户端保留实体但不渲染
    //原版客户端遇到未知类型同样保留实体只是没有模型 解码时直接抛异常会让整个包被丢掉
    private sealed class UnknownType(int rawId) : EntityType<object>
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("unknown");
        public override int RawId { get; } = rawId;
    }

    //ByIdOrUnknown 按网络序号解析实体类型 未注册时给保留原序号的占位
    public static EntityType<object> ByIdOrUnknown(int rawId)
        => ByRawId.TryGetValue(rawId, out var type) ? type : new UnknownType(rawId);

    //Register 注册实体类型到 ENTITY_TYPE 注册表并登记网络序号
    private static void Register(EntityType<object> type)
    {
        Registry<EntityType<object>>.Register(BuiltInRegistries.ENTITY_TYPE, type.Id, type);
        ByRawId[type.RawId] = type;
    }
}
