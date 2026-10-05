using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityTagPredicate 实体字符串标签谓词 判定实体标签集合的包含关系
//对应原版 net.minecraft.advancements.predicates.entity.EntityTagPredicate
public sealed record EntityTagPredicate(
    Optional<IReadOnlyList<string>> AnyOf,
    Optional<IReadOnlyList<string>> AllOf,
    Optional<IReadOnlyList<string>> NoneOf) : EntitySubPredicate
{
    //Codec 持久化编解码 字段名 any_of/all_of/none_of 对应原版 CODEC
    public static readonly Codec<EntityTagPredicate> Codec = RecordCodecBuilder.Of3(
        Codecs.String.ListOf().OptionalFieldOf("any_of")
            .ForGetter((EntityTagPredicate predicate) => predicate.AnyOf),
        Codecs.String.ListOf().OptionalFieldOf("all_of")
            .ForGetter((EntityTagPredicate predicate) => predicate.AllOf),
        Codecs.String.ListOf().OptionalFieldOf("none_of")
            .ForGetter((EntityTagPredicate predicate) => predicate.NoneOf),
        (anyOf, allOf, noneOf) => new EntityTagPredicate(anyOf, allOf, noneOf));

    //Matches 标签集合同时满足 any 与 none 与 all 三条约束 对应原版 matches
    public bool Matches(IReadOnlyCollection<string> tags)
    {
        if (AnyOf.IsPresent && !AnyOf.Get().Any(tags.Contains)) return false;
        if (NoneOf.IsPresent && NoneOf.Get().Any(tags.Contains)) return false;
        if (AllOf.IsPresent && !AllOf.Get().All(tags.Contains)) return false;
        return true;
    }

    //Matches 取实体标签后判定 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position) => Matches(entity.GetTags());
}
