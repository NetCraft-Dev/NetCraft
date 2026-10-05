using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//TrimPredicate 盔甲纹饰谓词 判定材料与图案是否落在给定集合
//对应原版 net.minecraft.core.component.predicates.TrimPredicate
public sealed record TrimPredicate(
    Optional<HolderSet<TrimMaterial>> Material,
    Optional<HolderSet<TrimPattern>> Pattern) : SingleComponentItemPredicate<ArmorTrim>
{
    //Codec 持久化编解码 两个可选集合字段 对应原版 CODEC
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
