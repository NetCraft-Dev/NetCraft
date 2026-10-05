using NetCraft.Codec;
using NetCraft.Game.World.Entity;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Registry;
using NetCraft.Registry.EntityAttribute;
//别名避开与所在命名空间末段 Component 撞名
using ChatComponent = NetCraft.Network.Chat.Component;

namespace NetCraft.Game.World.Items.Component;

//ItemAttributeModifiers 物品属性修饰条目列表 对应原版 net.minecraft.world.item.component.ItemAttributeModifiers
public sealed class ItemAttributeModifiers : IEquatable<ItemAttributeModifiers>
{
    //Empty 空列表 对应原版 EMPTY
    public static readonly ItemAttributeModifiers Empty = new(Array.Empty<Entry>());

    //Codec 持久化编解码 条目列表 对应原版 CODEC
    public static readonly Codec<ItemAttributeModifiers> Codec = Entry.Codec.ListOf().ComapFlatMap(
        list => DataResult<ItemAttributeModifiers>.Success(new ItemAttributeModifiers(list)),
        modifiers => modifiers.Modifiers);

    //StreamCodec 网络编解码 条目列表进出 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ItemAttributeModifiers> StreamCodec =
        new ItemAttributeModifiersStreamCodec();

    public ItemAttributeModifiers(IReadOnlyList<Entry> modifiers) => Modifiers = modifiers;

    public IReadOnlyList<Entry> Modifiers { get; }

    public bool Equals(ItemAttributeModifiers? other) => other is not null && Modifiers.SequenceEqual(other.Modifiers);

    public override bool Equals(object? obj) => Equals(obj as ItemAttributeModifiers);

    public override int GetHashCode() => Modifiers.Count;

    public override string ToString() => $"ItemAttributeModifiers[{Modifiers.Count} entries]";

    //Entry 属性修饰条目 属性引用加修饰符加生效槽位加显示方式
    public sealed record Entry(
        Holder<NetCraft.Registry.EntityAttribute.Attribute> Attribute,
        AttributeModifier Modifier,
        EquipmentSlotGroup Slot,
        Display DisplayData)
    {
        //Codec 持久化编解码 字段名 type 与 slot 与 display 对应原版 CODEC
        public static readonly Codec<Entry> Codec = RecordCodecBuilder.Of4(
            HolderSetCodecs.AttributeRef.FieldOf("type").ForGetter((Entry entry) => entry.Attribute),
            AttributeModifier.Codec.ForGetter((Entry entry) => entry.Modifier),
            EquipmentSlotGroups.Codec.OptionalFieldOf("slot", EquipmentSlotGroup.Any)
                .ForGetter((Entry entry) => entry.Slot),
            Display.Codec.OptionalFieldOf("display", Display.DefaultValue)
                .ForGetter((Entry entry) => entry.DisplayData),
            (attribute, modifier, slot, display) => new Entry(attribute, modifier, slot, display));

        //StreamCodec 网络编解码 对应原版 STREAM_CODEC
        public static readonly StreamCodec<RegistryFriendlyByteBuf, Entry> StreamCodec = new EntryStreamCodec();
    }
}

//Display 属性修饰的显示方式 对应原版 ItemAttributeModifiers.Display
//默认按数值生成提示 隐藏不显示 覆盖则用给定文本
public abstract class Display
{
    //DefaultValue 默认显示 对应原版 Default
    public static readonly Display DefaultValue = new DefaultDisplay();

    //HiddenValue 隐藏显示 对应原版 Hidden
    public static readonly Display HiddenValue = new HiddenDisplay();

    //Codec 按 type 字段分派 对应原版 Display.CODEC
    public static readonly Codec<Display> Codec = new DisplayCodec();

    //StreamCodec 按 type id 分派 对应原版 Display.STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, Display> StreamCodec = new DisplayStreamCodec();

    //TypeName 分派用的序列化名
    public abstract string TypeName { get; }

    //OverrideText 覆盖文本显示 对应原版 OverrideText
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

//DisplayCodec 按 type 字段分派显示方式 对应原版 Display.CODEC
internal sealed class DisplayCodec : ScalarCodec<Display>
{
    public override DataResult<Display> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var typeValue = map.Get("type");
            if (!typeValue.IsPresent) return DataResult<Display>.Error(() => "display 缺少 type 字段");
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
        if (!value.IsPresent) return DataResult<Display>.Error(() => "display 覆盖文本缺少 value 字段");
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

//DisplayStreamCodec 按 type id 分派 对应原版 Display.STREAM_CODEC
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

//AttributeModifierStreamCodec id 加数值加运算 对应原版 AttributeModifier.STREAM_CODEC
//AttributeModifier 属注册表层 该层不引用 Network 故流编解码落在这里
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

//EntryStreamCodec 属性引用加修饰符加槽位加显示方式 对应原版 STREAM_CODEC
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

//ItemAttributeModifiersStreamCodec 条目列表进出 对应原版 STREAM_CODEC
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
