using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Damage;

//DamageSource 伤害来源 对应原版 net.minecraft.world.damagesource.DamageSource
//持伤害类型 直接来源实体 真凶实体与伤害发生位置
//原版的死亡消息与武器附魔相关方法依赖活体与附魔体系 未接通故暂缺
public sealed class DamageSource
{
    public DamageSource(Holder<DamageType> type, NetCraft.Registry.Entity? directEntity,
        NetCraft.Registry.Entity? causingEntity, Vec3? damageSourcePosition = null)
    {
        TypeHolder = type;
        DirectEntity = directEntity;
        CausingEntity = causingEntity;
        DamageSourcePosition = damageSourcePosition ?? causingEntity?.Pos;
    }

    //TypeHolder 伤害类型句柄 对应原版 typeHolder
    public Holder<DamageType> TypeHolder { get; }

    //Type 伤害类型实体 对应原版 type
    public DamageType Type => TypeHolder.Value;

    //DirectEntity 直接造成伤害的实体 对应原版 getDirectEntity
    public NetCraft.Registry.Entity? DirectEntity { get; }

    //CausingEntity 伤害真凶 对应原版 getEntity
    public NetCraft.Registry.Entity? CausingEntity { get; }

    //DamageSourcePosition 伤害发生位置 对应原版 getSourcePosition
    public Vec3? DamageSourcePosition { get; }

    //Critical 是否暴击 对应原版 isCritical
    public bool Critical { get; init; }

    //IsDirect 直接来源与真凶是同一个 对应原版 isDirect
    public bool IsDirect() => ReferenceEquals(DirectEntity, CausingEntity);

    //GetMsgId 伤害消息键 对应原版 getMsgId
    public string GetMsgId() => Type.MessageId;

    //Is 伤害类型是否属于该标签 对应原版 is
    public bool Is(TagKey<DamageType> tag) => TypeHolder.Is(tag);

    //Is 伤害类型是否是该资源键 对应原版 is
    public bool Is(ResourceKey<DamageType> key) => TypeHolder.Is(key);

    //ScalesWithDifficulty 伤害是否随难度缩放 对应原版 scalesWithDifficulty
    //原版还要求来源不是玩家且类型带缩放标签 标签体系未接通故按缩放规则与有无真凶判定
    public bool ScalesWithDifficulty()
        => Type.Scaling != DamageScaling.NEVER && CausingEntity is not null;

    //IsCreativePlayer 是否创造模式玩家造成 对应原版 isCreativePlayer
    public bool IsCreativePlayer()
        => CausingEntity is NetCraft.Game.World.Entity.Player { GameMode: 1 };

    //GetWeaponItem 造成这次伤害用的武器 对应原版 getWeaponItem
    //实体的手持物品访问未接通 一律给空堆
    public ItemStack GetWeaponItem() => ItemStack.Empty;
}
