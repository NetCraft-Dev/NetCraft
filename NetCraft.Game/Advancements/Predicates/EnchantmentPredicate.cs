using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//EnchantmentPredicate single enchantment predicate, checks whether a matching enchantment and level exist in the enchantment table
//maps to vanilla net.minecraft.advancements.predicates.EnchantmentPredicate
public sealed record EnchantmentPredicate(Optional<HolderSet<Enchantment>> Enchantments, MinMaxBounds.Ints Level)
{
    //Codec persistence codec, field names enchantments/levels, maps to vanilla CODEC
    public static readonly Codec<EnchantmentPredicate> Codec = RecordCodecBuilder.Of2(
        HolderSetCodecs.EnchantmentSet.OptionalFieldOf("enchantments")
            .ForGetter((EnchantmentPredicate predicate) => predicate.Enchantments),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("levels", MinMaxBounds.Ints.Any)
            .ForGetter((EnchantmentPredicate predicate) => predicate.Level),
        (enchantments, level) => new EnchantmentPredicate(enchantments, level));

    //ContainedIn whether a matching entry exists in the enchantment table, maps to vanilla containedIn
    public bool ContainedIn(ItemEnchantments itemEnchantments)
    {
        if (Enchantments.IsPresent)
        {
            foreach (var enchantment in Enchantments.Get())
                if (MatchesEnchantment(itemEnchantments, enchantment)) return true;
            return false;
        }
        if (!ReferenceEquals(Level, MinMaxBounds.Ints.Any))
        {
            foreach (var level in itemEnchantments.Levels.Values)
                if (Level.Matches(level)) return true;
            return false;
        }
        return !itemEnchantments.IsEmpty;
    }

    //MatchesEnchantment the given enchantment level exists and falls in the range
    private bool MatchesEnchantment(ItemEnchantments itemEnchantments, Holder<Enchantment> enchantment)
    {
        var level = itemEnchantments.GetLevel(enchantment);
        return level != 0 && (ReferenceEquals(Level, MinMaxBounds.Ints.Any) || Level.Matches(level));
    }
}
