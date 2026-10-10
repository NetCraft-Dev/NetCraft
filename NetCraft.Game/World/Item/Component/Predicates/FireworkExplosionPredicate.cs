using NetCraft.Codec;
using NetCraft.Game.Advancements.Predicates;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//FireworkExplosionPredicate firework explosion predicate, checks the three toggles shape, trail and twinkle
//Maps to vanilla net.minecraft.core.component.predicates.FireworkExplosionPredicate
public sealed record FireworkExplosionPredicate(FireworkExplosionPredicate.FireworkPredicate Value)
    : SingleComponentItemPredicate<FireworkExplosion>
{
    //Codec persistence codec, the payload is just the three toggles, maps to vanilla CODEC
    public static readonly Codec<FireworkExplosionPredicate> Codec = FireworkPredicate.Codec.ComapFlatMap(
        predicate => DataResult<FireworkExplosionPredicate>.Success(new FireworkExplosionPredicate(predicate)),
        explosion => explosion.Value);

    public DataComponentType<object> ComponentType => DataComponents.FIREWORK_EXPLOSION;

    public bool MatchesValue(FireworkExplosion value) => Value.Test(value);

    //FireworkPredicate three-toggle predicate, absence means unconstrained, maps to vanilla FireworkPredicate
    public sealed record FireworkPredicate(
        Optional<bool> Shape,
        Optional<bool> Trail,
        Optional<bool> Twinkle) : IValuePredicate<FireworkExplosion>
    {
        public static readonly Codec<FireworkPredicate> Codec = RecordCodecBuilder.Of3(
            Codecs.Bool.OptionalFieldOf("shape").ForGetter((FireworkPredicate predicate) => predicate.Shape),
            Codecs.Bool.OptionalFieldOf("trail").ForGetter((FireworkPredicate predicate) => predicate.Trail),
            Codecs.Bool.OptionalFieldOf("twinkle").ForGetter((FireworkPredicate predicate) => predicate.Twinkle),
            (shape, trail, twinkle) => new FireworkPredicate(shape, trail, twinkle));

        public bool Test(FireworkExplosion value)
        {
            if (Shape.IsPresent && Shape.Get() != value.HasShape()) return false;
            if (Trail.IsPresent && Trail.Get() != value.Trail) return false;
            if (Twinkle.IsPresent && Twinkle.Get() != value.Twinkle) return false;
            return true;
        }
    }
}
