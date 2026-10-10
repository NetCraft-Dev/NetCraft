using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//EnchantmentsPredicate base class for enchantment predicates, every enchantment predicate in the list must hold
//Maps to vanilla net.minecraft.core.component.predicates.EnchantmentsPredicate
public abstract class EnchantmentsPredicate : SingleComponentItemPredicate<ItemEnchantments>
{
    protected EnchantmentsPredicate(IReadOnlyList<EnchantmentPredicate> enchantments)
        => EnchantmentPredicates = enchantments;

    //EnchantmentPredicates list of enchantment predicates used for matching; the property cannot share the name of the nested class Enchantments
    public IReadOnlyList<EnchantmentPredicate> EnchantmentPredicates { get; }

    //CodecOf list codec plus constructor, maps to vanilla codec
    public static Codec<T> CodecOf<T>(Func<IReadOnlyList<EnchantmentPredicate>, T> constructor)
        where T : EnchantmentsPredicate
        => EnchantmentPredicate.Codec.ListOf().ComapFlatMap(
            list => DataResult<T>.Success(constructor(list)),
            predicate => predicate.EnchantmentPredicates);

    public abstract DataComponentType<object> ComponentType { get; }

    //MatchesValue every enchantment predicate must find a match in the enchantment map
    public bool MatchesValue(ItemEnchantments appliedEnchantments)
    {
        foreach (var enchantment in EnchantmentPredicates)
            if (!enchantment.ContainedIn(appliedEnchantments)) return false;
        return true;
    }

    //Enchantments plain enchantment predicate, maps to vanilla Enchantments
    public sealed class Enchantments : EnchantmentsPredicate
    {
        public static readonly Codec<Enchantments> Codec = CodecOf<Enchantments>(list => new Enchantments(list));

        public Enchantments(IReadOnlyList<EnchantmentPredicate> enchantments) : base(enchantments) { }

        public override DataComponentType<object> ComponentType => DataComponents.ENCHANTMENTS;
    }

    //StoredEnchantments stored enchantments predicate, maps to vanilla StoredEnchantments
    public sealed class StoredEnchantments : EnchantmentsPredicate
    {
        public static readonly Codec<StoredEnchantments> Codec =
            CodecOf<StoredEnchantments>(list => new StoredEnchantments(list));

        public StoredEnchantments(IReadOnlyList<EnchantmentPredicate> enchantments) : base(enchantments) { }

        public override DataComponentType<object> ComponentType => DataComponents.STORED_ENCHANTMENTS;
    }
}
