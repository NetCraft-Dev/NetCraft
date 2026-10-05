using NetCraft.Codec;
using NetCraft.Registry.State;

namespace NetCraft.Game.Advancements.Predicates;

//StatePropertiesPredicate 方块状态属性谓词 逐条判定状态上的属性取值
//对应原版 net.minecraft.advancements.predicates.StatePropertiesPredicate
public sealed record StatePropertiesPredicate(
    IReadOnlyList<StatePropertiesPredicate.PropertyMatcher> Properties)
{
    //Codec 属性名到值匹配的映射 对应原版 CODEC
    public static readonly Codec<StatePropertiesPredicate> Codec = Codecs.UnboundedMap(
        Codecs.String, ValueMatcher.Codec).ComapFlatMap(
        map => DataResult<StatePropertiesPredicate>.Success(new StatePropertiesPredicate(
            map.Select(entry => new PropertyMatcher(entry.Key, entry.Value)).ToList())),
        predicate => predicate.Properties.ToDictionary(
            matcher => matcher.Name, matcher => matcher.ValueMatcher));

    //Matches 每条属性都要匹配上 对应原版 matches
    //本作 BlockState 自带属性集合 属性存在性直接查状态即可
    public bool Matches(BlockState state)
    {
        foreach (var matcher in Properties)
        {
            var property = FindProperty(state, matcher.Name);
            if (property is null || !matcher.Match(state, property)) return false;
        }
        return true;
    }

    //FindProperty 在状态的属性集合里按名字找 对应原版 StateDefinition.getProperty
    private static PropertyBase? FindProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property.Name == name) return property;
        return null;
    }

    //PropertyMatcher 单条属性匹配 对应原版 PropertyMatcher
    public sealed record PropertyMatcher(string Name, ValueMatcher ValueMatcher)
    {
        //Match 取状态上该属性的实际值再交给值匹配 对应原版 match
        public bool Match(BlockState state, PropertyBase property)
        {
            foreach (var entry in state.GetValues())
                if (entry.Property.Equals(property))
                    return ValueMatcher.Matches(entry.Value, property);
            return false;
        }
    }

    //ValueMatcher 属性值匹配 精确或区间 对应原版 ValueMatcher
    public abstract record ValueMatcher
    {
        //Codec 先按精确解析 失败退回区间 对应原版 CODEC
        public static readonly Codec<ValueMatcher> Codec = Codecs.Either(
            ExactMatcher.Codec, RangedMatcher.Codec).ComapFlatMap(
            alt => DataResult<ValueMatcher>.Success(
                alt.Map<ValueMatcher>(exact => exact, ranged => ranged)),
            matcher => matcher is ExactMatcher exact
                ? Alt<ExactMatcher, RangedMatcher>.Left(exact)
                : Alt<ExactMatcher, RangedMatcher>.Right((RangedMatcher)matcher));

        //Matches 按属性的合法取值判定 对应原版 match
        public abstract bool Matches(object actual, PropertyBase property);
    }

    //ExactMatcher 精确等值 对应原版 ExactMatcher
    public sealed record ExactMatcher(string Value) : ValueMatcher
    {
        //Codec 单个属性值名 对应原版 CODEC
        public static readonly Codec<ExactMatcher> Codec = Codecs.String.ComapFlatMap(
            value => DataResult<ExactMatcher>.Success(new ExactMatcher(value)),
            matcher => matcher.Value);

        //Matches 实际值的名字与期望一致 名字非法时永远不等
        public override bool Matches(object actual, PropertyBase property)
            => property.GetNameForValue(actual) == Value;
    }

    //RangedMatcher 区间匹配 对应原版 RangedMatcher
    public sealed record RangedMatcher(Optional<string> MinValue, Optional<string> MaxValue) : ValueMatcher
    {
        //Codec 字段名 min 与 max 对应原版 CODEC
        public static readonly Codec<RangedMatcher> Codec = RecordCodecBuilder.Of2(
            Codecs.String.OptionalFieldOf("min")
                .ForGetter((RangedMatcher matcher) => matcher.MinValue),
            Codecs.String.OptionalFieldOf("max")
                .ForGetter((RangedMatcher matcher) => matcher.MaxValue),
            (minValue, maxValue) => new RangedMatcher(minValue, maxValue));

        //Matches 实际值落在上下界之间 边界名取不到即不匹配 对应原版 match
        public override bool Matches(object actual, PropertyBase property)
        {
            var comparable = (IComparable)actual;
            if (MinValue.IsPresent)
            {
                var minValue = property.GetValueForName(MinValue.Get());
                if (minValue is null || comparable.CompareTo(minValue) < 0) return false;
            }
            if (MaxValue.IsPresent)
            {
                var maxValue = property.GetValueForName(MaxValue.Get());
                if (maxValue is null || comparable.CompareTo(maxValue) > 0) return false;
            }
            return true;
        }
    }
}
