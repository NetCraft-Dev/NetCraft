using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.World.Items;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//ContainerPredicate 容器谓词 判定容器内物品集合是否满足集合谓词
//对应原版 net.minecraft.core.component.predicates.ContainerPredicate
public sealed record ContainerPredicate(CollectionPredicate<ItemStack, ItemPredicate> Contents)
    : SingleComponentItemPredicate<ItemContainerContents>
{
    //Codec 持久化编解码 本体就是集合谓词 对应原版 CODEC
    public static readonly Codec<ContainerPredicate> Codec =
        CollectionPredicate<ItemStack, ItemPredicate>.Codec(ItemPredicate.Codec).ComapFlatMap(
            predicate => DataResult<ContainerPredicate>.Success(new ContainerPredicate(predicate)),
            container => container.Contents);

    public DataComponentType<object> ComponentType => DataComponents.CONTAINER;

    //MatchesValue 只看非空槽 物化成物品栈再交给集合谓词
    public bool MatchesValue(ItemContainerContents value) => Contents.Test(value.NonEmptyItems().ToList());
}
