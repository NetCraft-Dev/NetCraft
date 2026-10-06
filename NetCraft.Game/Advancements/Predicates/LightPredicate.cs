using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//LightPredicate light predicate, checks whether the position's maximum local brightness falls in a range
//maps to vanilla net.minecraft.advancements.predicates.LightPredicate
public sealed record LightPredicate(MinMaxBounds.Ints Composite)
{
    //Codec persistence codec, field name light, maps to vanilla CODEC
    public static readonly Codec<LightPredicate> Codec = RecordCodecBuilder.Of1(
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("light", MinMaxBounds.Ints.Any)
            .ForGetter((LightPredicate predicate) => predicate.Composite),
        composite => new LightPredicate(composite));

    //Matches the position is loaded and the brightness falls in the range, maps to vanilla matches
    public bool Matches(ILevelReader level, BlockPos pos)
        => level.IsLoaded(pos) && Composite.Matches(level.GetMaxLocalRawBrightness(pos));
}
