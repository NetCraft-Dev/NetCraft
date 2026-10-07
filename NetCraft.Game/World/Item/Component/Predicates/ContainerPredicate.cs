using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.World.Items;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//ContainerPredicate container predicate, checks whether the items in the container satisfy the collection predicate
//Maps to vanilla net.minecraft.core.component.predicates.ContainerPredicate
public sealed record ContainerPredicate(CollectionPredicate<ItemStack, ItemPredicate> Contents)
    : SingleComponentItemPredicate<ItemContainerContents>
{
    //Codec persistence codec, the payload is just the collection predicate, maps to vanilla CODEC
    public static readonly Codec<ContainerPredicate> Codec =
        CollectionPredicate<ItemStack, ItemPredicate>.Codec(ItemPredicate.Codec).ComapFlatMap(
            predicate => DataResult<ContainerPredicate>.Success(new ContainerPredicate(predicate)),
            container => container.Contents);

    public DataComponentType<object> ComponentType => DataComponents.CONTAINER;

    //MatchesValue looks only at non-empty slots, materializes them into item stacks and hands them to the collection predicate
    public bool MatchesValue(ItemContainerContents value) => Contents.Test(value.NonEmptyItems().ToList());
}
