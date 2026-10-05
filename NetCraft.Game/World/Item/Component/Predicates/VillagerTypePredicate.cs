using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//VillagerTypePredicate 村民变体谓词 判定变体引用是否落在给定集合里
//对应原版 net.minecraft.core.component.predicates.VillagerTypePredicate
public sealed record VillagerTypePredicate(HolderSet<VillagerType> VillagerTypes)
    : SingleComponentItemPredicate<Holder<VillagerType>>
{
    //Codec 持久化编解码 本体就是村民类型集合 对应原版 CODEC
    public static readonly Codec<VillagerTypePredicate> Codec = HolderSetCodecs.VillagerTypeSet.ComapFlatMap(
        set => DataResult<VillagerTypePredicate>.Success(new VillagerTypePredicate(set)),
        predicate => predicate.VillagerTypes);

    public DataComponentType<object> ComponentType => DataComponents.VILLAGER_VARIANT;

    public bool MatchesValue(Holder<VillagerType> value) => VillagerTypes.Contains(value);
}
