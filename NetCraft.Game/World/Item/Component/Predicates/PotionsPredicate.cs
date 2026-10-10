using NetCraft.Codec;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//PotionsPredicate potion predicate, checks whether the base potion in the contents falls in the given set
//Maps to vanilla net.minecraft.core.component.predicates.PotionsPredicate
public sealed record PotionsPredicate(HolderSet<Potion> Potions) : SingleComponentItemPredicate<PotionContents>
{
    //Codec persistence codec, the payload is just the potion set, maps to vanilla CODEC
    public static readonly Codec<PotionsPredicate> Codec = HolderSetCodecs.PotionSet.ComapFlatMap(
        set => DataResult<PotionsPredicate>.Success(new PotionsPredicate(set)),
        predicate => predicate.Potions);

    public DataComponentType<object> ComponentType => DataComponents.POTION_CONTENTS;

    //MatchesValue without a base potion it does not match, maps to the vanilla potion().isPresent() guard
    public bool MatchesValue(PotionContents value)
        => value.Potion.IsPresent && Potions.Contains(value.Potion.Get());
}
