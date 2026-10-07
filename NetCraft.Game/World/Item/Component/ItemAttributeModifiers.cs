using NetCraft.Codec;
using NetCraft.Game.World.Entity;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Registry;
using NetCraft.Registry.EntityAttribute;
//Alias avoids clashing with Component, the last segment of the enclosing namespace
using ChatComponent = NetCraft.Network.Chat.Component;

namespace NetCraft.Game.World.Items.Component;

//ItemAttributeModifiers list of item attribute modifier entries, maps to vanilla net.minecraft.world.item.component.ItemAttributeModifiers
public sealed class ItemAttributeModifiers : IEquatable<ItemAttributeModifiers>
{
    //Empty empty list, maps to vanilla EMPTY
    public static readonly ItemAttributeModifiers Empty = new(Array.Empty<Entry>());

    //Codec persistence codec, a list of entries, maps to vanilla CODEC
    public static readonly Codec<ItemAttributeModifiers> Codec = Entry.Codec.ListOf().ComapFlatMap(
        list => DataResult<ItemAttributeModifiers>.Success(new ItemAttributeModifiers(list)),
        modifiers => modifiers.Modifiers);

    //StreamCodec network codec, the entry list goes in and out, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemAttributeModifiers> StreamCodec =
        new ItemAttributeModifiersStreamCodec();

    public ItemAttributeModifiers(IReadOnlyList<Entry> modifiers) => Modifiers = modifiers;

    public IReadOnlyList<Entry> Modifiers { get; }

    public bool Equals(ItemAttributeModifiers? other) => other is not null && Modifiers.SequenceEqual(other.Modifiers);

    public override bool Equals(object? obj) => Equals(obj as ItemAttributeModifiers);

    public override int GetHashCode() => Modifiers.Count;

    public override string ToString() => $"ItemAttributeModifiers[{Modifiers.Count} entries]";

    //Entry attribute modifier entry: attribute reference plus modifier plus active slots plus display
    public sealed record Entry(
        Holder<NetCraft.Registry.EntityAttribute.Attribute> Attribute,
        AttributeModifier Modifier,
        EquipmentSlotGroup Slot,
        Display DisplayData)
    {
        //Codec persistence codec, field names type, slot and display, maps to vanilla CODEC
        public static readonly Codec<Entry> Codec = RecordCodecBuilder.Of4(
            HolderSetCodecs.AttributeRef.FieldOf("type").ForGetter((Entry entry) => entry.Attribute),
            AttributeModifier.Codec.ForGetter((Entry entry) => entry.Modifier),
            EquipmentSlotGroups.Codec.OptionalFieldOf("slot", EquipmentSlotGroup.Any)
                .ForGetter((Entry entry) => entry.Slot),
            Display.Codec.OptionalFieldOf("display", Display.DefaultValue)
                .ForGetter((Entry entry) => entry.DisplayData),
            (attribute, modifier, slot, display) => new Entry(attribute, modifier, slot, display));

        //StreamCodec network codec, maps to vanilla STREAM_CODEC
        public static readonly StreamCodec<RegistryFriendlyByteBuf, Entry> StreamCodec = new EntryStreamCodec();
    }
}

//Display how an attribute modifier is shown, maps to vanilla ItemAttributeModifiers.Display
//Default builds a tooltip from the value, hidden shows nothing, override uses the given text
public abstract class Display
{
    //DefaultValue default display, maps to vanilla Default
    public static readonly Display DefaultValue = new DefaultDisplay();

    //HiddenValue hidden display, maps to vanilla Hidden
    public static readonly Display HiddenValue = new HiddenDisplay();

    //Codec dispatches on the type field, maps to vanilla Display.CODEC
    public static readonly Codec<Display> Codec = new DisplayCodec();

    //StreamCodec dispatches on the type id, maps to vanilla Display.STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, Display> StreamCodec = new DisplayStreamCodec();

    //TypeName serialized name used for dispatch
    public abstract string TypeName { get; }

    //OverrideText override text display, maps to vanilla OverrideText
    public static Display OverrideText(ChatComponent component) => new OverrideTextDisplay(component);

    internal sealed class DefaultDisplay : Display
    {
        public override string TypeName => "default";
    }

    internal sealed class HiddenDisplay : Display
    {
        public override string TypeName => "hidden";
    }

    internal sealed class OverrideTextDisplay(ChatComponent component) : Display
    {
        public ChatComponent Component { get; } = component;

        public override string TypeName => "override";
    }
}

//DisplayCodec dispatches the display on the type field, maps to vanilla Display.CODEC
internal sealed class DisplayCodec : ScalarCodec<Display>
{
    public override DataResult<Display> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var typeValue = map.Get("type");
            if (!typeValue.IsPresent) return DataResult<Display>.Error(() => "display is missing the type field");
            return Codecs.String.Parse(ops, typeValue.Get()).FlatMap(name => name switch
            {
                "override" => ParseOverrideText(ops, map),
                "hidden" => DataResult<Display>.Success(Display.HiddenValue),
                _ => DataResult<Display>.Success(Display.DefaultValue)
            });
        });

    private static DataResult<Display> ParseOverrideText<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var value = map.Get("value");
        if (!value.IsPresent) return DataResult<Display>.Error(() => "display override text is missing the value field");
        return ComponentSerialization.Codec.Parse(ops, value.Get()).Map(Display.OverrideText);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Display value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.TypeName));
        if (value is Display.OverrideTextDisplay overrideText)
            builder.Add("value", ComponentSerialization.Codec.EncodeStart(ops, overrideText.Component).GetOrThrow());
        return builder.Build(ops.Empty());
    }
}

//DisplayStreamCodec dispatches on the type id, maps to vanilla Display.STREAM_CODEC
internal sealed class DisplayStreamCodec : StreamCodec<RegistryFriendlyByteBuf, Display>
{
    public Display Decode(RegistryFriendlyByteBuf buf)
    {
        var type = buf.ReadVarInt();
        return type switch
        {
            2 => Display.OverrideText(ComponentSerialization.StreamCodec.Decode(buf)),
            1 => Display.HiddenValue,
            _ => Display.DefaultValue
        };
    }

    public void Encode(RegistryFriendlyByteBuf buf, Display value)
    {
        switch (value)
        {
            case Display.OverrideTextDisplay overrideText:
                buf.WriteVarInt(2);
                ComponentSerialization.StreamCodec.Encode(buf, overrideText.Component);
                break;
            case Display.HiddenDisplay:
                buf.WriteVarInt(1);
                break;
            default:
                buf.WriteVarInt(0);
                break;
        }
    }
}

//AttributeModifierStreamCodec id plus amount plus operation, maps to vanilla AttributeModifier.STREAM_CODEC
//AttributeModifier is a registry-layer type; that layer does not reference Network so the stream codec lives here
internal sealed class AttributeModifierStreamCodec : StreamCodec<RegistryFriendlyByteBuf, AttributeModifier>
{
    public static readonly AttributeModifierStreamCodec Instance = new();

    public AttributeModifier Decode(RegistryFriendlyByteBuf buf)
        => new(buf.ReadIdentifier(), buf.ReadDouble(), (AttributeOperation)buf.ReadVarInt());

    public void Encode(RegistryFriendlyByteBuf buf, AttributeModifier value)
    {
        buf.WriteIdentifier(value.Id);
        buf.WriteDouble(value.Amount);
        buf.WriteVarInt((int)value.Operation);
    }
}

//EntryStreamCodec attribute reference plus modifier plus slots plus display, maps to vanilla STREAM_CODEC
internal sealed class EntryStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemAttributeModifiers.Entry>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<NetCraft.Registry.EntityAttribute.Attribute>> AttributeCodec =
        ByteBufCodecs.Holder(Registries.ATTRIBUTE);

    public ItemAttributeModifiers.Entry Decode(RegistryFriendlyByteBuf buf)
        => new(
            AttributeCodec.Decode(buf),
            AttributeModifierStreamCodec.Instance.Decode(buf),
            EquipmentSlotGroups.StreamCodec.Decode(buf),
            Display.StreamCodec.Decode(buf));

    public void Encode(RegistryFriendlyByteBuf buf, ItemAttributeModifiers.Entry value)
    {
        AttributeCodec.Encode(buf, value.Attribute);
        AttributeModifierStreamCodec.Instance.Encode(buf, value.Modifier);
        EquipmentSlotGroups.StreamCodec.Encode(buf, value.Slot);
        Display.StreamCodec.Encode(buf, value.DisplayData);
    }
}

//ItemAttributeModifiersStreamCodec the entry list goes in and out, maps to vanilla STREAM_CODEC
internal sealed class ItemAttributeModifiersStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ItemAttributeModifiers>
{
    public ItemAttributeModifiers Decode(RegistryFriendlyByteBuf buf)
    {
        var size = buf.ReadVarInt();
        var modifiers = new List<ItemAttributeModifiers.Entry>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) modifiers.Add(ItemAttributeModifiers.Entry.StreamCodec.Decode(buf));
        return new ItemAttributeModifiers(modifiers);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ItemAttributeModifiers value)
    {
        buf.WriteVarInt(value.Modifiers.Count);
        foreach (var entry in value.Modifiers) ItemAttributeModifiers.Entry.StreamCodec.Encode(buf, entry);
    }
}
