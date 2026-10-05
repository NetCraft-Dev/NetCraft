using NetCraft.Codec;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityEquipmentPredicate 实体装备谓词 用物品谓词逐个槽位判定
//对应原版 net.minecraft.advancements.predicates.entity.EntityEquipmentPredicate
public sealed record EntityEquipmentPredicate(
    Optional<ItemPredicate> Head,
    Optional<ItemPredicate> Chest,
    Optional<ItemPredicate> Legs,
    Optional<ItemPredicate> Feet,
    Optional<ItemPredicate> Body,
    Optional<ItemPredicate> Mainhand,
    Optional<ItemPredicate> Offhand) : EntitySubPredicate
{
    //Codec 持久化编解码 字段名 head chest legs feet body mainhand offhand 对应原版 CODEC
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

    //Matches 非装备持有者判否 否则逐槽位判定 未给出的槽位跳过 对应原版 matches
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
