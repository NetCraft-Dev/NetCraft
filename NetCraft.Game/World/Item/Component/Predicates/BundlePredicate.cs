using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.World.Items;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//BundlePredicate 收纳袋谓词 判定袋内物品集合是否满足集合谓词
//对应原版 net.minecraft.core.component.predicates.BundlePredicate
public sealed record BundlePredicate(CollectionPredicate<ItemStack, ItemPredicate> Contents)
    : SingleComponentItemPredicate<BundleContents>
{
    //Codec 持久化编解码 本体就是集合谓词 对应原版 CODEC
    public static readonly Codec<BundlePredicate> Codec =
        CollectionPredicate<ItemStack, ItemPredicate>.Codec(ItemPredicate.Codec).ComapFlatMap(
            predicate => DataResult<BundlePredicate>.Success(new BundlePredicate(predicate)),
            bundle => bundle.Contents);

    public DataComponentType<object> ComponentType => DataComponents.BUNDLE_CONTENTS;

    //MatchesValue 袋内存的是模板 先物化成物品栈再交给集合谓词
    public bool MatchesValue(BundleContents value) => Contents.Test(value.ItemCopyStream().ToList());
}
