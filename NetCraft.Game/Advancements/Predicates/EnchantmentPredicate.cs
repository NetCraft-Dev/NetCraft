using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//EnchantmentPredicate 单条附魔谓词 判定附魔表里是否存在匹配的附魔与等级
//对应原版 net.minecraft.advancements.predicates.EnchantmentPredicate
public sealed record EnchantmentPredicate(Optional<HolderSet<Enchantment>> Enchantments, MinMaxBounds.Ints Level)
{
    //Codec 持久化编解码 字段名 enchantments 与 levels 对应原版 CODEC
    public static readonly Codec<EnchantmentPredicate> Codec = RecordCodecBuilder.Of2(
        HolderSetCodecs.EnchantmentSet.OptionalFieldOf("enchantments")
            .ForGetter((EnchantmentPredicate predicate) => predicate.Enchantments),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("levels", MinMaxBounds.Ints.Any)
            .ForGetter((EnchantmentPredicate predicate) => predicate.Level),
        (enchantments, level) => new EnchantmentPredicate(enchantments, level));

    //ContainedIn 附魔表里是否存在匹配项 对应原版 containedIn
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

    //MatchesEnchantment 指定附魔等级存在且落在区间
    private bool MatchesEnchantment(ItemEnchantments itemEnchantments, Holder<Enchantment> enchantment)
    {
        var level = itemEnchantments.GetLevel(enchantment);
        return level != 0 && (ReferenceEquals(Level, MinMaxBounds.Ints.Any) || Level.Matches(level));
    }
}
