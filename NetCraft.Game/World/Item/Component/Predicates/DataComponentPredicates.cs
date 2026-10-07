using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//DataComponentPredicates data component predicate type registration, maps to vanilla net.minecraft.core.component.predicates.DataComponentPredicates
//Bootstrap is called together with DataComponents.Bootstrap, must run before the registry is frozen
public static class DataComponentPredicates
{
    //ATTRIBUTE_MODIFIERS attribute modifier predicate
    public static readonly ConcreteType<AttributeModifiersPredicate> ATTRIBUTE_MODIFIERS =
        Register("attribute_modifiers", AttributeModifiersPredicate.Codec);

    //BUNDLE_CONTENTS bundle contents predicate
    public static readonly ConcreteType<BundlePredicate> BUNDLE_CONTENTS =
        Register("bundle_contents", BundlePredicate.Codec);

    //CONTAINER container predicate
    public static readonly ConcreteType<ContainerPredicate> CONTAINER =
        Register("container", ContainerPredicate.Codec);

    //CUSTOM_DATA custom data predicate
    public static readonly ConcreteType<CustomDataPredicate> CUSTOM_DATA =
        Register("custom_data", CustomDataPredicate.Codec);

    //DAMAGE durability and damage predicate
    public static readonly ConcreteType<DamagePredicate> DAMAGE = Register("damage", DamagePredicate.Codec);

    //ENCHANTMENTS enchantments predicate
    public static readonly ConcreteType<EnchantmentsPredicate.Enchantments> ENCHANTMENTS =
        Register("enchantments", EnchantmentsPredicate.Enchantments.Codec);

    //STORED_ENCHANTMENTS stored enchantments predicate
    public static readonly ConcreteType<EnchantmentsPredicate.StoredEnchantments> STORED_ENCHANTMENTS =
        Register("stored_enchantments", EnchantmentsPredicate.StoredEnchantments.Codec);

    //FIREWORK_EXPLOSION firework explosion predicate
    public static readonly ConcreteType<FireworkExplosionPredicate> FIREWORK_EXPLOSION =
        Register("firework_explosion", FireworkExplosionPredicate.Codec);

    //FIREWORKS firework rocket predicate
    public static readonly ConcreteType<FireworksPredicate> FIREWORKS =
        Register("fireworks", FireworksPredicate.Codec);

    //JUKEBOX_PLAYABLE jukebox playable predicate
    public static readonly ConcreteType<JukeboxPlayablePredicate> JUKEBOX_PLAYABLE =
        Register("jukebox_playable", JukeboxPlayablePredicate.Codec);

    //POTION_CONTENTS potion contents predicate
    public static readonly ConcreteType<PotionsPredicate> POTION_CONTENTS =
        Register("potion_contents", PotionsPredicate.Codec);

    //TRIM armor trim predicate
    public static readonly ConcreteType<TrimPredicate> TRIM =
        Register("trim", TrimPredicate.Codec);

    //VILLAGER_VARIANT villager variant predicate
    public static readonly ConcreteType<VillagerTypePredicate> VILLAGER_VARIANT =
        Register("villager_variant", VillagerTypePredicate.Codec);

    //WRITABLE_BOOK_CONTENT writable book predicate
    public static readonly ConcreteType<WritableBookPredicate> WRITABLE_BOOK_CONTENT =
        Register("writable_book_content", WritableBookPredicate.Codec);

    //WRITTEN_BOOK_CONTENT written book predicate
    public static readonly ConcreteType<WrittenBookPredicate> WRITTEN_BOOK_CONTENT =
        Register("written_book_content", WrittenBookPredicate.Codec);

    //Bootstrap triggers static field initialization to complete the registration
    public static void Bootstrap()
    {
        _ = ATTRIBUTE_MODIFIERS;
        _ = BUNDLE_CONTENTS;
        _ = CONTAINER;
        _ = CUSTOM_DATA;
        _ = DAMAGE;
        _ = ENCHANTMENTS;
        _ = STORED_ENCHANTMENTS;
        _ = FIREWORK_EXPLOSION;
        _ = FIREWORKS;
        _ = JUKEBOX_PLAYABLE;
        _ = POTION_CONTENTS;
        _ = TRIM;
        _ = VILLAGER_VARIANT;
        _ = WRITABLE_BOOK_CONTENT;
        _ = WRITTEN_BOOK_CONTENT;
    }

    //Register registers a predicate type into the DATA_COMPONENT_PREDICATE_TYPE registry
    private static ConcreteType<T> Register<T>(string name, Codec<T> codec)
        where T : class, DataComponentPredicate
    {
        var type = new ConcreteType<T>(codec);
        Registry<DataComponentPredicateType<object>>.Register(
            BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE, name, type);
        return type;
    }
}
