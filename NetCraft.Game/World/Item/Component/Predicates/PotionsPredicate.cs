using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//PotionsPredicate 药水谓词 判定内容里的基础药水是否落在给定集合
//对应原版 net.minecraft.core.component.predicates.PotionsPredicate
public sealed record PotionsPredicate(HolderSet<Potion> Potions) : SingleComponentItemPredicate<PotionContents>
{
    //Codec 持久化编解码 本体就是药水集合 对应原版 CODEC
    public static readonly Codec<PotionsPredicate> Codec = HolderSetCodecs.PotionSet.ComapFlatMap(
        set => DataResult<PotionsPredicate>.Success(new PotionsPredicate(set)),
        predicate => predicate.Potions);

    public DataComponentType<object> ComponentType => DataComponents.POTION_CONTENTS;

    //MatchesValue 没有基础药水直接不匹配 对应原版 potion().isPresent() 前置判断
    public bool MatchesValue(PotionContents value)
        => value.Potion.IsPresent && Potions.Contains(value.Potion.Get());
}
