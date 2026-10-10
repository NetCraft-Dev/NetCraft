using NetCraft.Codec;
using NetCraft.DataFixer.Util;
using NetCraft.Game.World.Items.Component;
using NetCraft.Game.World.Items.Component.Predicates;
using NetCraft.Network;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//DataComponents predefined component types, maps to vanilla net.minecraft.core.component.DataComponents
//Simplified: only simple value components are implemented, using object boxing to match the Java Integer/Boolean/Unit boxing semantics
//The persistence codec is used by the command-layer item component syntax; non-persistent components pass null like vanilla to stay transient
//Complex business-type components (FoodProperties/Tool/Weapon/Enchantments/ItemLore etc.) are completed once the business subsystems are ready
//Bootstrap is called by ClientMain/ServerMain after NetCraftKernel.Initialize
public static class DataComponents
{
    //MAX_STACK_SIZE item max stack size
    public static readonly DataComponentType<object> MAX_STACK_SIZE = Register(
        "max_stack_size", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //MAX_DAMAGE item max durability
    public static readonly DataComponentType<object> MAX_DAMAGE = Register(
        "max_damage", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //DAMAGE item current damage
    public static readonly DataComponentType<object> DAMAGE = Register(
        "damage", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //REPAIR_COST repair cost
    public static readonly DataComponentType<object> REPAIR_COST = Register(
        "repair_cost", IntObjectNbtCodec.Instance, new VarIntObjectCodec());

    //UNBREAKABLE unbreakable flag
    public static readonly DataComponentType<object> UNBREAKABLE = Register(
        "unbreakable", UnitObjectNbtCodec.Instance, new UnitObjectCodec());

    //CREATIVE_SLOT_LOCK creative slot lock; vanilla has no persistence codec so it stays transient
    public static readonly DataComponentType<object> CREATIVE_SLOT_LOCK = Register(
        "creative_slot_lock", null, new UnitObjectCodec());

    //INTANGIBLE_PROJECTILE intangible projectile
    public static readonly DataComponentType<object> INTANGIBLE_PROJECTILE = Register(
        "intangible_projectile", UnitObjectNbtCodec.Instance, new UnitObjectCodec());

    //ENCHANTMENT_GLINT_OVERRIDE enchantment glint override
    public static readonly DataComponentType<object> ENCHANTMENT_GLINT_OVERRIDE = Register(
        "enchantment_glint_override", BooleanObjectNbtCodec.Instance, new BooleanObjectCodec());

    //BEES bees stored in a bee nest
    public static readonly DataComponentType<object> BEES = Register(
        "bees", new ObjectCodec<Bees>(Bees.Codec), new ObjectStreamCodec<Bees>(Bees.StreamCodec));

    //BUNDLE_CONTENTS bundle contents
    public static readonly DataComponentType<object> BUNDLE_CONTENTS = Register(
        "bundle_contents", new ObjectCodec<BundleContents>(BundleContents.PersistentCodec), new ObjectStreamCodec<BundleContents>(BundleContents.StreamCodec));

    //BLOCK_ENTITY_DATA block entity data carried by a block item, flushed into the new block entity when the block is placed
    public static readonly DataComponentType<object> BLOCK_ENTITY_DATA = Register(
        "block_entity_data",
        new ObjectCodec<TypedEntityData<Holder<BlockEntityType<object>>>>(
            TypedEntityData<Holder<BlockEntityType<object>>>.CodecOf(HolderSetCodecs.BlockEntityTypeRef)),
        new ObjectStreamCodec<TypedEntityData<Holder<BlockEntityType<object>>>>(
            TypedEntityData<Holder<BlockEntityType<object>>>.StreamCodecOf(
                ByteBufCodecs.Holder(Registries.BLOCK_ENTITY_TYPE))));

    //CUSTOM_DATA free-form custom data; item NBT predicates use it to match contents
    public static readonly DataComponentType<object> CUSTOM_DATA = Register(
        "custom_data",
        new ObjectCodec<CustomData>(CustomData.PersistentCodec),
        new ObjectStreamCodec<CustomData>(CustomData.StreamCodec));

    //FIREWORK_EXPLOSION firework explosion effect
    public static readonly DataComponentType<object> FIREWORK_EXPLOSION = Register(
        "firework_explosion",
        new ObjectCodec<FireworkExplosion>(FireworkExplosion.Codec),
        new ObjectStreamCodec<FireworkExplosion>(FireworkExplosion.StreamCodec));

    //FIREWORKS firework rocket data
    public static readonly DataComponentType<object> FIREWORKS = Register(
        "fireworks",
        new ObjectCodec<Fireworks>(Fireworks.Codec),
        new ObjectStreamCodec<Fireworks>(Fireworks.StreamCodec));

    //POTION_CONTENTS potion contents
    public static readonly DataComponentType<object> POTION_CONTENTS = Register(
        "potion_contents",
        new ObjectCodec<PotionContents>(PotionContents.Codec),
        new ObjectStreamCodec<PotionContents>(PotionContents.StreamCodec));

    //CONTAINER item container contents
    public static readonly DataComponentType<object> CONTAINER = Register(
        "container",
        new ObjectCodec<ItemContainerContents>(ItemContainerContents.Codec),
        new ObjectStreamCodec<ItemContainerContents>(ItemContainerContents.StreamCodec));

    //ATTRIBUTE_MODIFIERS item attribute modifier entries
    public static readonly DataComponentType<object> ATTRIBUTE_MODIFIERS = Register(
        "attribute_modifiers",
        new ObjectCodec<ItemAttributeModifiers>(ItemAttributeModifiers.Codec),
        new ObjectStreamCodec<ItemAttributeModifiers>(ItemAttributeModifiers.StreamCodec));

    //ENCHANTMENTS item enchantment map
    public static readonly DataComponentType<object> ENCHANTMENTS = Register(
        "enchantments",
        new ObjectCodec<ItemEnchantments>(ItemEnchantments.Codec),
        new ObjectStreamCodec<ItemEnchantments>(ItemEnchantments.StreamCodec));

    //STORED_ENCHANTMENTS enchantment map stored in an enchanted book
    public static readonly DataComponentType<object> STORED_ENCHANTMENTS = Register(
        "stored_enchantments",
        new ObjectCodec<ItemEnchantments>(ItemEnchantments.Codec),
        new ObjectStreamCodec<ItemEnchantments>(ItemEnchantments.StreamCodec));

    //TRIM armor trim
    public static readonly DataComponentType<object> TRIM = Register(
        "trim",
        new ObjectCodec<ArmorTrim>(ArmorTrim.Codec),
        new ObjectStreamCodec<ArmorTrim>(ArmorTrim.StreamCodec));

    //JUKEBOX_PLAYABLE jukebox playable track
    public static readonly DataComponentType<object> JUKEBOX_PLAYABLE = Register(
        "jukebox_playable",
        new ObjectCodec<JukeboxPlayable>(JukeboxPlayable.Codec),
        new ObjectStreamCodec<JukeboxPlayable>(JukeboxPlayable.StreamCodec));

    //WRITABLE_BOOK_CONTENT book and quill contents
    public static readonly DataComponentType<object> WRITABLE_BOOK_CONTENT = Register(
        "writable_book_content",
        new ObjectCodec<WritableBookContent>(WritableBookContent.Codec),
        new ObjectStreamCodec<WritableBookContent>(WritableBookContent.StreamCodec));

    //WRITTEN_BOOK_CONTENT written book contents
    public static readonly DataComponentType<object> WRITTEN_BOOK_CONTENT = Register(
        "written_book_content",
        new ObjectCodec<WrittenBookContent>(WrittenBookContent.Codec),
        new ObjectStreamCodec<WrittenBookContent>(WrittenBookContent.StreamCodec));

    //VILLAGER_VARIANT villager variant, the value is a reference into the villager type registry
    public static readonly DataComponentType<object> VILLAGER_VARIANT = Register(
        "villager_variant",
        new ObjectCodec<Holder<VillagerType>>(HolderSetCodecs.VillagerTypeRef),
        new ObjectStreamCodec<Holder<VillagerType>>(ByteBufCodecs.Holder(Registries.VILLAGER_TYPE)));

    //Register registers a single component type into BuiltInRegistries.DATA_COMPONENT_TYPE
    private static DataComponentType<object> Register(
        string name, Codec<object>? codec, StreamCodec<RegistryFriendlyByteBuf, object> streamCodec)
    {
        var type = new SimpleDataComponentType<object>(codec, streamCodec, false);
        Registry<object>.Register(BuiltInRegistries.DATA_COMPONENT_TYPE, name, type);
        return type;
    }

    //Bootstrap registers all predefined component types, called by ClientMain/ServerMain
    //C# static fields initialize on first access to the class, so this method forces the trigger to guarantee the registration timing
    public static void Bootstrap()
    {
        _ = MAX_STACK_SIZE;
        _ = BEES;
        _ = BUNDLE_CONTENTS;
        _ = BLOCK_ENTITY_DATA;
        _ = FIREWORK_EXPLOSION;
        _ = FIREWORKS;
        _ = POTION_CONTENTS;
        _ = CONTAINER;
        _ = ATTRIBUTE_MODIFIERS;
        _ = ENCHANTMENTS;
        _ = STORED_ENCHANTMENTS;
        _ = TRIM;
        _ = JUKEBOX_PLAYABLE;
        _ = WRITABLE_BOOK_CONTENT;
        _ = WRITTEN_BOOK_CONTENT;
        _ = VILLAGER_VARIANT;
        DataComponentPredicates.Bootstrap();
    }
}

//ObjectStreamCodec adapts the stream codec of a reference type into an object version for DataComponents to register complex components
internal sealed class ObjectStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, object> where T : class
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, T> _inner;

    public ObjectStreamCodec(StreamCodec<RegistryFriendlyByteBuf, T> inner) => _inner = inner;

    public object Decode(RegistryFriendlyByteBuf buf) => _inner.Decode(buf);

    public void Encode(RegistryFriendlyByteBuf buf, object value) => _inner.Encode(buf, (T)value);
}

//ObjectCodec adapts the persistence codec of a reference type into an object version, paired with ObjectStreamCodec
internal sealed class ObjectCodec<T> : ScalarCodec<object> where T : class
{
    private readonly Codec<T> _inner;

    public ObjectCodec(Codec<T> inner) => _inner = inner;

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input) => _inner.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value) => _inner.EncodeStart(ops, (T)value);
}

//VarIntObjectCodec StreamCodec for int boxed as object, encoding with VarInt
internal sealed class VarIntObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => buf.ReadVarInt();

    public void Encode(RegistryFriendlyByteBuf buf, object value) => buf.WriteVarInt((int)value);
}

//BooleanObjectCodec StreamCodec for bool boxed as object, reads 1 byte
internal sealed class BooleanObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => buf.ReadBoolean();

    public void Encode(RegistryFriendlyByteBuf buf, object value) => buf.WriteBoolean((bool)value);
}

//UnitObjectCodec StreamCodec for Unit boxed as object, no payload and decoding returns Unit.Instance
internal sealed class UnitObjectCodec : StreamCodec<RegistryFriendlyByteBuf, object>
{
    public object Decode(RegistryFriendlyByteBuf buf) => Unit.Instance;

    public void Encode(RegistryFriendlyByteBuf buf, object value) { }
}

//IntObjectNbtCodec persistence codec for int components, converting to and from NBT Int
internal sealed class IntObjectNbtCodec : ScalarCodec<object>
{
    public static readonly IntObjectNbtCodec Instance = new();

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => Codecs.Int.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => Codecs.Int.EncodeStart(ops, (int)value);
}

//BooleanObjectNbtCodec persistence codec for bool components, converting to and from NBT Byte
internal sealed class BooleanObjectNbtCodec : ScalarCodec<object>
{
    public static readonly BooleanObjectNbtCodec Instance = new();

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => Codecs.Bool.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => Codecs.Bool.EncodeStart(ops, (bool)value);
}

//UnitObjectNbtCodec persistence codec for unit components, matching vanilla Unit.CODEC which always parses and encodes to empty
internal sealed class UnitObjectNbtCodec : ScalarCodec<object>
{
    public static readonly UnitObjectNbtCodec Instance = new();

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<object>.Success(Unit.Instance);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => DataResult<U>.Success(ops.Empty());
}
