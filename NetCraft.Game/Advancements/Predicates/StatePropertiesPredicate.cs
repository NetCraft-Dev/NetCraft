using NetCraft.Codec;
using NetCraft.Registry.State;

namespace NetCraft.Game.Advancements.Predicates;

//StatePropertiesPredicate block state properties predicate, checks the property values on a state item by item
//maps to vanilla net.minecraft.advancements.predicates.StatePropertiesPredicate
public sealed record StatePropertiesPredicate(
    IReadOnlyList<StatePropertiesPredicate.PropertyMatcher> Properties)
{
    //Codec mapping from property name to a value matcher, maps to vanilla CODEC
    public static readonly Codec<StatePropertiesPredicate> Codec = Codecs.UnboundedMap(
        Codecs.String, ValueMatcher.Codec).ComapFlatMap(
        map => DataResult<StatePropertiesPredicate>.Success(new StatePropertiesPredicate(
            map.Select(entry => new PropertyMatcher(entry.Key, entry.Value)).ToList())),
        predicate => predicate.Properties.ToDictionary(
            matcher => matcher.Name, matcher => matcher.ValueMatcher));

    //Matches every property must match, maps to vanilla matches
    //BlockState carries its property set here, so property existence can be checked on the state directly
    public bool Matches(BlockState state)
    {
        foreach (var matcher in Properties)
        {
            var property = FindProperty(state, matcher.Name);
            if (property is null || !matcher.Match(state, property)) return false;
        }
        return true;
    }

    //FindProperty looks a property up by name in the state's property set, maps to vanilla StateDefinition.getProperty
    private static PropertyBase? FindProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property.Name == name) return property;
        return null;
    }

    //PropertyMatcher single property match, maps to vanilla PropertyMatcher
    public sealed record PropertyMatcher(string Name, ValueMatcher ValueMatcher)
    {
        //Match reads the actual value on the state and hands it to the value matcher, maps to vanilla match
        public bool Match(BlockState state, PropertyBase property)
        {
            foreach (var entry in state.GetValues())
                if (entry.Property.Equals(property))
                    return ValueMatcher.Matches(entry.Value, property);
            return false;
        }
    }

    //ValueMatcher property value match, exact or ranged, maps to vanilla ValueMatcher
    public abstract record ValueMatcher
    {
        //Codec parses as exact first and falls back to ranged, maps to vanilla CODEC
        public static readonly Codec<ValueMatcher> Codec = Codecs.Either(
            ExactMatcher.Codec, RangedMatcher.Codec).ComapFlatMap(
            alt => DataResult<ValueMatcher>.Success(
                alt.Map<ValueMatcher>(exact => exact, ranged => ranged)),
            matcher => matcher is ExactMatcher exact
                ? Alt<ExactMatcher, RangedMatcher>.Left(exact)
                : Alt<ExactMatcher, RangedMatcher>.Right((RangedMatcher)matcher));

        //Matches decided against the property's legal values, maps to vanilla match
        public abstract bool Matches(object actual, PropertyBase property);
    }

    //ExactMatcher exact equality, maps to vanilla ExactMatcher
    public sealed record ExactMatcher(string Value) : ValueMatcher
    {
        //Codec a single property value name, maps to vanilla CODEC
        public static readonly Codec<ExactMatcher> Codec = Codecs.String.ComapFlatMap(
            value => DataResult<ExactMatcher>.Success(new ExactMatcher(value)),
            matcher => matcher.Value);

        //Matches the actual value's name equals the expected one; never equal when the name is invalid
        public override bool Matches(object actual, PropertyBase property)
            => property.GetNameForValue(actual) == Value;
    }

    //RangedMatcher ranged match, maps to vanilla RangedMatcher
    public sealed record RangedMatcher(Optional<string> MinValue, Optional<string> MaxValue) : ValueMatcher
    {
        //Codec field names min/max, maps to vanilla CODEC
        public static readonly Codec<RangedMatcher> Codec = RecordCodecBuilder.Of2(
            Codecs.String.OptionalFieldOf("min")
                .ForGetter((RangedMatcher matcher) => matcher.MinValue),
            Codecs.String.OptionalFieldOf("max")
                .ForGetter((RangedMatcher matcher) => matcher.MaxValue),
            (minValue, maxValue) => new RangedMatcher(minValue, maxValue));

        //Matches the actual value falls between the bounds; no match when a bound name is unknown, maps to vanilla match
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
