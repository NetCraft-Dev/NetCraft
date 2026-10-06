using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityFlagsPredicate entity flags predicate, compares the entity's boolean flags item by item; fields without a value always pass
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityFlagsPredicate
public sealed record EntityFlagsPredicate(
    Optional<bool> IsOnGround,
    Optional<bool> IsOnFire,
    Optional<bool> IsCrouching,
    Optional<bool> IsSprinting,
    Optional<bool> IsSwimming,
    Optional<bool> IsFlying,
    Optional<bool> IsBaby,
    Optional<bool> IsInWater,
    Optional<bool> IsFallFlying) : EntitySubPredicate
{
    //Codec persistence codec, field names such as is_on_ground align with vanilla CODEC
    public static readonly Codec<EntityFlagsPredicate> Codec = RecordCodecBuilder.Of9(
        Codecs.Bool.OptionalFieldOf("is_on_ground")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsOnGround),
        Codecs.Bool.OptionalFieldOf("is_on_fire")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsOnFire),
        Codecs.Bool.OptionalFieldOf("is_sneaking")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsCrouching),
        Codecs.Bool.OptionalFieldOf("is_sprinting")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsSprinting),
        Codecs.Bool.OptionalFieldOf("is_swimming")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsSwimming),
        Codecs.Bool.OptionalFieldOf("is_flying")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsFlying),
        Codecs.Bool.OptionalFieldOf("is_baby")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsBaby),
        Codecs.Bool.OptionalFieldOf("is_in_water")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsInWater),
        Codecs.Bool.OptionalFieldOf("is_fall_flying")
            .ForGetter((EntityFlagsPredicate predicate) => predicate.IsFallFlying),
        (isOnGround, isOnFire, isCrouching, isSprinting, isSwimming, isFlying, isBaby, isInWater, isFallFlying) =>
            new EntityFlagsPredicate(isOnGround, isOnFire, isCrouching, isSprinting, isSwimming, isFlying, isBaby,
                isInWater, isFallFlying));

    //Matches flags compared item by item, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity)
    {
        if (IsOnGround.IsPresent && entity.OnGround != IsOnGround.Get()) return false;
        if (IsOnFire.IsPresent && entity.IsOnFire != IsOnFire.Get()) return false;
        if (IsCrouching.IsPresent && entity.IsCrouching != IsCrouching.Get()) return false;
        if (IsSprinting.IsPresent && entity.IsSprinting != IsSprinting.Get()) return false;
        if (IsSwimming.IsPresent && entity.IsSwimming != IsSwimming.Get()) return false;
        if (IsFlying.IsPresent && entity.IsFlying != IsFlying.Get()) return false;
        if (IsBaby.IsPresent && entity.IsBaby != IsBaby.Get()) return false;
        if (IsInWater.IsPresent && entity.IsInWater != IsInWater.Get()) return false;
        if (IsFallFlying.IsPresent && entity.IsFallFlying != IsFallFlying.Get()) return false;
        return true;
    }

    //Matches the interface implementation that ignores the position argument, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position) => Matches(entity);
}
