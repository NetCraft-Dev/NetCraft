using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Damage;

//DamageSource damage source, maps to vanilla net.minecraft.world.damagesource.DamageSource
//Holds the damage type, direct entity, culprit entity and the damage position
//Vanilla's death message and weapon enchantment related methods depend on the living entity and enchantment systems; not wired up, so they are omitted
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

    //TypeHolder damage type handle, maps to vanilla typeHolder
    public Holder<DamageType> TypeHolder { get; }

    //Type damage type entity, maps to vanilla type
    public DamageType Type => TypeHolder.Value;

    //DirectEntity the entity dealing the damage directly, maps to vanilla getDirectEntity
    public NetCraft.Registry.Entity? DirectEntity { get; }

    //CausingEntity the damage culprit, maps to vanilla getEntity
    public NetCraft.Registry.Entity? CausingEntity { get; }

    //DamageSourcePosition the damage position, maps to vanilla getSourcePosition
    public Vec3? DamageSourcePosition { get; }

    //Critical whether it is a crit, maps to vanilla isCritical
    public bool Critical { get; init; }

    //IsDirect the direct source and the culprit are the same, maps to vanilla isDirect
    public bool IsDirect() => ReferenceEquals(DirectEntity, CausingEntity);

    //GetMsgId damage message key, maps to vanilla getMsgId
    public string GetMsgId() => Type.MessageId;

    //Is whether the damage type belongs to the tag, maps to vanilla is
    public bool Is(TagKey<DamageType> tag) => TypeHolder.Is(tag);

    //Is whether the damage type is the resource key, maps to vanilla is
    public bool Is(ResourceKey<DamageType> key) => TypeHolder.Is(key);

    //ScalesWithDifficulty whether damage scales with difficulty, maps to vanilla scalesWithDifficulty
    //Vanilla also requires the source not to be a player and the type to carry a scaling tag; the tag system is not wired up, so it is decided by the scaling rule and whether a culprit exists
    public bool ScalesWithDifficulty()
        => Type.Scaling != DamageScaling.NEVER && CausingEntity is not null;

    //IsCreativePlayer whether it was caused by a creative-mode player, maps to vanilla isCreativePlayer
    public bool IsCreativePlayer()
        => CausingEntity is NetCraft.Game.World.Entity.Player { GameMode: 1 };

    //GetWeaponItem the weapon used for this damage, maps to vanilla getWeaponItem
    //The entity's held item access is not wired up, so an empty stack is returned
    public ItemStack GetWeaponItem() => ItemStack.Empty;
}
