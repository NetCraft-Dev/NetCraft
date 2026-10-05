using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//EnchantmentsPredicate 附魔谓词基类 列表里每条附魔谓词都要成立
//对应原版 net.minecraft.core.component.predicates.EnchantmentsPredicate
public abstract class EnchantmentsPredicate : SingleComponentItemPredicate<ItemEnchantments>
{
    protected EnchantmentsPredicate(IReadOnlyList<EnchantmentPredicate> enchantments) => Enchantments = enchantments;

    //Enchantments 判定用的附魔谓词列表
    public IReadOnlyList<EnchantmentPredicate> Enchantments { get; }

    //CodecOf 列表编解码加构造器 对应原版 codec
    public static Codec<T> CodecOf<T>(Func<IReadOnlyList<EnchantmentPredicate>, T> constructor)
        where T : EnchantmentsPredicate
        => EnchantmentPredicate.Codec.ListOf().ComapFlatMap(
            list => DataResult<T>.Success(constructor(list)),
            predicate => predicate.Enchantments);

    public abstract DataComponentType<object> ComponentType { get; }

    //MatchesValue 逐条附魔谓词都要在附魔表里找到匹配
    public bool MatchesValue(ItemEnchantments appliedEnchantments)
    {
        foreach (var enchantment in Enchantments)
            if (!enchantment.ContainedIn(appliedEnchantments)) return false;
        return true;
    }

    //Enchantments 普通附魔谓词 对应原版 Enchantments
    public sealed class Enchantments : EnchantmentsPredicate
    {
        public static readonly Codec<Enchantments> Codec = CodecOf<Enchantments>(list => new Enchantments(list));

        public Enchantments(IReadOnlyList<EnchantmentPredicate> enchantments) : base(enchantments) { }

        public override DataComponentType<object> ComponentType => DataComponents.ENCHANTMENTS;
    }

    //StoredEnchantments 附魔书谓词 对应原版 StoredEnchantments
    public sealed class StoredEnchantments : EnchantmentsPredicate
    {
        public static readonly Codec<StoredEnchantments> Codec =
            CodecOf<StoredEnchantments>(list => new StoredEnchantments(list));

        public StoredEnchantments(IReadOnlyList<EnchantmentPredicate> enchantments) : base(enchantments) { }

        public override DataComponentType<object> ComponentType => DataComponents.STORED_ENCHANTMENTS;
    }
}
