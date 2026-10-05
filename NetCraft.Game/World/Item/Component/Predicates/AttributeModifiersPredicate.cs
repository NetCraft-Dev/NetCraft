using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Entity;
using NetCraft.Network.Component;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.EntityAttribute;

namespace NetCraft.Game.World.Items.Component.Predicates;

//AttributeModifiersPredicate 属性修饰谓词 判定修饰条目集合是否满足集合谓词
//对应原版 net.minecraft.core.component.predicates.AttributeModifiersPredicate
public sealed record AttributeModifiersPredicate(
    Optional<CollectionPredicate<ItemAttributeModifiers.Entry, AttributeModifiersPredicate.EntryPredicate>> Modifiers)
    : SingleComponentItemPredicate<ItemAttributeModifiers>
{
    //Codec 持久化编解码 只有 modifiers 一个字段 对应原版 CODEC
    public static readonly Codec<AttributeModifiersPredicate> Codec = RecordCodecBuilder.Of1(
        CollectionPredicate<ItemAttributeModifiers.Entry, EntryPredicate>.Codec(EntryPredicate.Codec)
            .OptionalFieldOf("modifiers")
            .ForGetter((AttributeModifiersPredicate predicate) => predicate.Modifiers),
        modifiers => new AttributeModifiersPredicate(modifiers));

    public DataComponentType<object> ComponentType => DataComponents.ATTRIBUTE_MODIFIERS;

    public bool MatchesValue(ItemAttributeModifiers value)
        => !Modifiers.IsPresent || Modifiers.Get().Test(value.Modifiers);

    //EntryPredicate 单条修饰匹配 属性集合加 id 加数值区间加运算加槽位
    //对应原版 EntryPredicate
    public sealed record EntryPredicate(
        Optional<HolderSet<NetCraft.Registry.EntityAttribute.Attribute>> Attributes,
        Optional<Identifier> Id,
        MinMaxBounds.Doubles Amount,
        Optional<AttributeOperation> Operation,
        Optional<EquipmentSlotGroup> Slot) : IValuePredicate<ItemAttributeModifiers.Entry>
    {
        //Codec 持久化编解码 字段名 attribute 与 id 与 amount 与 operation 与 slot 对应原版 CODEC
        public static readonly Codec<EntryPredicate> Codec = RecordCodecBuilder.Of5(
            HolderSetCodecs.AttributeSet.OptionalFieldOf("attribute")
                .ForGetter((EntryPredicate predicate) => predicate.Attributes),
            IdentifierCodec.Instance.OptionalFieldOf("id").ForGetter((EntryPredicate predicate) => predicate.Id),
            MinMaxBounds.Doubles.CODEC.OptionalFieldOf("amount", MinMaxBounds.Doubles.Any)
                .ForGetter((EntryPredicate predicate) => predicate.Amount),
            AttributeModifier.OperationCodec.OptionalFieldOf("operation")
                .ForGetter((EntryPredicate predicate) => predicate.Operation),
            EquipmentSlotGroups.Codec.OptionalFieldOf("slot").ForGetter((EntryPredicate predicate) => predicate.Slot),
            (attributes, id, amount, operation, slot)
                => new EntryPredicate(attributes, id, amount, operation, slot));

        public bool Test(ItemAttributeModifiers.Entry value)
        {
            if (Attributes.IsPresent && !Attributes.Get().Contains(value.Attribute)) return false;
            if (Id.IsPresent && !Id.Get().Equals(value.Modifier.Id)) return false;
            if (!Amount.Matches(value.Modifier.Amount)) return false;
            if (Operation.IsPresent && Operation.Get() != value.Modifier.Operation) return false;
            if (Slot.IsPresent && Slot.Get() != value.Slot) return false;
            return true;
        }
    }
}
