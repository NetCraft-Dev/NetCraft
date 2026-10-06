using NetCraft.Codec;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityEquipmentPredicate entity equipment predicate, checks each slot with the item predicate
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityEquipmentPredicate
public sealed record EntityEquipmentPredicate(
    Optional<ItemPredicate> Head,
    Optional<ItemPredicate> Chest,
    Optional<ItemPredicate> Legs,
    Optional<ItemPredicate> Feet,
    Optional<ItemPredicate> Body,
    Optional<ItemPredicate> Mainhand,
    Optional<ItemPredicate> Offhand) : EntitySubPredicate
{
    //Codec persistence codec, field names head chest legs feet body mainhand offhand, maps to vanilla CODEC
    public static readonly Codec<EntityEquipmentPredicate> Codec = RecordCodecBuilder.Of7(
        ItemPredicate.Codec.OptionalFieldOf("head")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Head),
        ItemPredicate.Codec.OptionalFieldOf("chest")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Chest),
        ItemPredicate.Codec.OptionalFieldOf("legs")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Legs),
        ItemPredicate.Codec.OptionalFieldOf("feet")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Feet),
        ItemPredicate.Codec.OptionalFieldOf("body")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Body),
        ItemPredicate.Codec.OptionalFieldOf("mainhand")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Mainhand),
        ItemPredicate.Codec.OptionalFieldOf("offhand")
            .ForGetter((EntityEquipmentPredicate predicate) => predicate.Offhand),
        (head, chest, legs, feet, body, mainhand, offhand) =>
            new EntityEquipmentPredicate(head, chest, legs, feet, body, mainhand, offhand));

    //Matches a non-equipment holder fails, otherwise checked per slot; slots without an expectation are skipped, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        if (entity is not IEquipmentHolder holder) return false;
        if (Head.IsPresent && !Head.Get().Test(holder.GetItemBySlot(EquipmentSlot.HEAD))) return false;
        if (Chest.IsPresent && !Chest.Get().Test(holder.GetItemBySlot(EquipmentSlot.CHEST))) return false;
        if (Legs.IsPresent && !Legs.Get().Test(holder.GetItemBySlot(EquipmentSlot.LEGS))) return false;
        if (Feet.IsPresent && !Feet.Get().Test(holder.GetItemBySlot(EquipmentSlot.FEET))) return false;
        if (Body.IsPresent && !Body.Get().Test(holder.GetItemBySlot(EquipmentSlot.BODY))) return false;
        if (Mainhand.IsPresent && !Mainhand.Get().Test(holder.GetItemBySlot(EquipmentSlot.MAINHAND))) return false;
        if (Offhand.IsPresent && !Offhand.Get().Test(holder.GetItemBySlot(EquipmentSlot.OFFHAND))) return false;
        return true;
    }
}
