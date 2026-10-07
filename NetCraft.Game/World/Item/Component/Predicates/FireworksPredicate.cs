using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//FireworksPredicate firework rocket predicate, checks the explosion set and flight duration
//Maps to vanilla net.minecraft.core.component.predicates.FireworksPredicate
public sealed record FireworksPredicate(
    Optional<CollectionPredicate<FireworkExplosion, FireworkExplosionPredicate.FireworkPredicate>> Explosions,
    Optional<MinMaxBounds.Ints> FlightDuration) : SingleComponentItemPredicate<Fireworks>
{
    //Codec persistence codec, field names explosions and flight_duration, maps to vanilla CODEC
    public static readonly Codec<FireworksPredicate> Codec = RecordCodecBuilder.Of2(
        CollectionPredicate<FireworkExplosion, FireworkExplosionPredicate.FireworkPredicate>
            .Codec(FireworkExplosionPredicate.FireworkPredicate.Codec)
            .OptionalFieldOf("explosions")
            .ForGetter((FireworksPredicate predicate) => predicate.Explosions),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("flight_duration")
            .ForGetter((FireworksPredicate predicate) => predicate.FlightDuration),
        (explosions, flightDuration) => new FireworksPredicate(explosions, flightDuration));

    public DataComponentType<object> ComponentType => DataComponents.FIREWORKS;

    public bool MatchesValue(Fireworks value)
    {
        if (FlightDuration.IsPresent && !FlightDuration.Get().Matches(value.FlightDuration)) return false;
        return !Explosions.IsPresent || Explosions.Get().Test(value.Explosions);
    }
}
