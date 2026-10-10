using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.EntityAttribute;

namespace NetCraft.Game.World.Items.Component.Predicates;

//AttributeModifiersPredicate attribute modifier predicate, checks whether the modifier entries satisfy the collection predicate
//Maps to vanilla net.minecraft.core.component.predicates.AttributeModifiersPredicate
public sealed record AttributeModifiersPredicate(
    Optional<CollectionPredicate<ItemAttributeModifiers.Entry, AttributeModifiersPredicate.EntryPredicate>> Modifiers)
    : SingleComponentItemPredicate<ItemAttributeModifiers>
{
    //Codec persistence codec, only a modifiers field, maps to vanilla CODEC
    public static readonly Codec<AttributeModifiersPredicate> Codec = RecordCodecBuilder.Of1(
        CollectionPredicate<ItemAttributeModifiers.Entry, EntryPredicate>.Codec(EntryPredicate.Codec)
            .OptionalFieldOf("modifiers")
            .ForGetter((AttributeModifiersPredicate predicate) => predicate.Modifiers),
        modifiers => new AttributeModifiersPredicate(modifiers));

    public DataComponentType<object> ComponentType => DataComponents.ATTRIBUTE_MODIFIERS;

    public bool MatchesValue(ItemAttributeModifiers value)
        => !Modifiers.IsPresent || Modifiers.Get().Test(value.Modifiers);

    //EntryPredicate single modifier match: attribute set plus id plus amount range plus operation plus slots
    //Maps to vanilla EntryPredicate
    public sealed record EntryPredicate(
        Optional<HolderSet<NetCraft.Registry.EntityAttribute.Attribute>> Attributes,
        Optional<Identifier> Id,
        MinMaxBounds.Doubles Amount,
        Optional<AttributeOperation> Operation,
        Optional<EquipmentSlotGroup> Slot) : IValuePredicate<ItemAttributeModifiers.Entry>
    {
        //Codec persistence codec, field names attribute, id, amount, operation and slot, maps to vanilla CODEC
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
