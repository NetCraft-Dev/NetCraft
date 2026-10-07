using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//TrimPredicate armor trim predicate, checks whether material and pattern fall in the given sets
//Maps to vanilla net.minecraft.core.component.predicates.TrimPredicate
public sealed record TrimPredicate(
    Optional<HolderSet<TrimMaterial>> Material,
    Optional<HolderSet<TrimPattern>> Pattern) : SingleComponentItemPredicate<ArmorTrim>
{
    //Codec persistence codec, two optional set fields, maps to vanilla CODEC
    public static readonly Codec<TrimPredicate> Codec = RecordCodecBuilder.Of2(
        HolderSetCodecs.TrimMaterialSet.OptionalFieldOf("material")
            .ForGetter((TrimPredicate predicate) => predicate.Material),
        HolderSetCodecs.TrimPatternSet.OptionalFieldOf("pattern")
            .ForGetter((TrimPredicate predicate) => predicate.Pattern),
        (material, pattern) => new TrimPredicate(material, pattern));

    public DataComponentType<object> ComponentType => DataComponents.TRIM;

    public bool MatchesValue(ArmorTrim value)
        => !(Material.IsPresent && !Material.Get().Contains(value.Material)
             || Pattern.IsPresent && !Pattern.Get().Contains(value.Pattern));
}
