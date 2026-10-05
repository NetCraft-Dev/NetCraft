using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityPredicate 实体谓词组合体 把若干子谓词按类型名合并成一次判定
//对应原版 net.minecraft.advancements.predicates.entity.EntityPredicate
//原版 wrap 与 createContext 依赖 loot 体系 该体系未接通 这里只落判定与组合
public sealed class EntityPredicate
{
    //MapCodec 类型名到子谓词的映射编解码 值 codec 按名从注册表取 对应原版 MAP_CODEC
    private static readonly Codec<Dictionary<Identifier, EntitySubPredicate>> MapCodec =
        Codecs.DispatchedMap(IdentifierCodec.Instance, LookupCodec);

    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<EntityPredicate> Codec = MapCodec.ComapFlatMap(
        parts => DataResult<EntityPredicate>.Success(new EntityPredicate(parts)),
        predicate => new Dictionary<Identifier, EntitySubPredicate>(predicate.Parts));

    //Parts 子谓词集合 键是注册表里的类型名
    public IReadOnlyDictionary<Identifier, EntitySubPredicate> Parts { get; }

    //_combinedPart 合并后的单一子谓词 判定走它
    private readonly EntitySubPredicate _combinedPart;

    public EntityPredicate(Dictionary<Identifier, EntitySubPredicate> parts)
    {
        Parts = parts;
        _combinedPart = Combine(parts);
    }

    //Matches 实体非空时交给合并子谓词 对应原版 matches
    public bool Matches(ILevelReader? level, Vec3? position, NetCraft.Registry.Entity? entity)
        => entity is not null && _combinedPart.Matches(entity, level, position);

    //LookupCodec 按类型名取注册表里已登记的 codec 未登记给出报错 codec
    private static Codec<EntitySubPredicate> LookupCodec(Identifier typeName)
        => BuiltInRegistries.ENTITY_SUB_PREDICATE_TYPE.GetValue(typeName)
            ?? new UnknownSubPredicateCodec(typeName.ToString());

    //Combine 空映射恒真 单个直接返回 多个按类型优先级排序后合取 对应原版 combine
    private static EntitySubPredicate Combine(Dictionary<Identifier, EntitySubPredicate> parts)
    {
        if (parts.Count == 0) return TrueSubPredicate.Instance;
        if (parts.Count == 1) return parts.Values.First();
        var ordered = parts.OrderBy(entry => OrderOf(entry.Key)).Select(entry => entry.Value).ToList();
        return new CompositeSubPredicate(ordered);
    }

    //OrderOf 类型谓词排最前 nbt 排最后 其余居中 对应原版 PREDICATE_TYPE_ORDER
    private static int OrderOf(Identifier typeName)
    {
        var path = typeName.Path;
        if (path == "entity_type") return -1;
        if (path == "nbt") return 2;
        return 0;
    }

    //Builder 组合体构造器 对应原版 Builder
    public sealed class Builder
    {
        private readonly Dictionary<Identifier, EntitySubPredicate> _parts = new();

        public static Builder Entity() => new();

        //Put 按注册名登记子谓词 对应原版 put
        public Builder Put(string typeName, EntitySubPredicate predicate)
        {
            _parts[Identifier.Parse(typeName)] = predicate;
            return this;
        }

        //EntityType 登记实体类型谓词 对应原版 entityType
        public Builder EntityType(EntityTypePredicate predicate) => Put("entity_type", predicate);

        //Tags 登记实体标签谓词 对应原版 entity_tags
        public Builder Tags(EntityTagPredicate predicate) => Put("entity_tags", predicate);

        //Flags 登记状态位谓词 对应原版 flags
        public Builder Flags(EntityFlagsPredicate predicate) => Put("flags", predicate);

        //Nbt 登记 NBT 谓词 对应原版 nbt
        public Builder Nbt(EntityNbtPredicate predicate) => Put("nbt", predicate);

        //Moving 登记移动谓词 对应原版 moving
        public Builder Moving(MovementPredicate predicate) => Put("movement", predicate);

        //Distance 登记与发起者距离谓词 对应原版 distance
        public Builder Distance(DistanceToPlayerPredicate predicate) => Put("distance", predicate);

        //PeriodicTick 登记周期刻谓词 对应原版 periodicTick
        public Builder PeriodicTick(PeriodicEntityTickPredicate predicate) => Put("periodic_tick", predicate);

        //Located 登记实体所在位置谓词 对应原版 located
        public Builder Located(LocationPredicate location) => Put("location", new EntityLocationPredicate(location));

        //SteppingOn 登记踩踏位置谓词 对应原版 steppingOn
        public Builder SteppingOn(LocationPredicate location) => Put("stepping_on", new SteppingOnPredicate(location));

        //MovementAffectedBy 登记移动受影响谓词 对应原版 movementAffectedBy
        public Builder MovementAffectedBy(LocationPredicate location)
            => Put("movement_affected_by", new MovementAffectedByPredicate(location));

        //Equipment 登记装备谓词 对应原版 equipment
        public Builder Equipment(EntityEquipmentPredicate equipment) => Put("equipment", equipment);

        //Build 产出组合体 对应原版 build
        public EntityPredicate Build() => new(_parts);
    }
}

//TrueSubPredicate 恒真子谓词 对应原版 EntitySubPredicate.ALWAYS_TRUE
internal sealed class TrueSubPredicate : EntitySubPredicate
{
    //Instance 唯一实例
    public static readonly TrueSubPredicate Instance = new();

    private TrueSubPredicate() { }

    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position) => true;
}

//CompositeSubPredicate 多个子谓词的合取 对应原版 combine 多于两个时的合并
internal sealed class CompositeSubPredicate(IReadOnlyList<EntitySubPredicate> parts) : EntitySubPredicate
{
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        foreach (var part in parts)
            if (!part.Matches(entity, level, position)) return false;
        return true;
    }
}

//UnknownSubPredicateCodec 未登记的子谓词类型 解析与编码都报错
internal sealed class UnknownSubPredicateCodec(string typeName) : ScalarCodec<EntitySubPredicate>
{
    public override DataResult<EntitySubPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<EntitySubPredicate>.Error(() => $"未知实体子谓词类型 {typeName}");

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, EntitySubPredicate value)
        => DataResult<U>.Error(() => $"未知实体子谓词类型 {typeName}");
}
