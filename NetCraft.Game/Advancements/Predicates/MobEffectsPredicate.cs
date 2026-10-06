using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Effect;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//MobEffectsPredicate mob effect predicate, checks the mob effects on the entity item by item
//maps to vanilla net.minecraft.advancements.predicates.MobEffectsPredicate
public sealed record MobEffectsPredicate(
    Dictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectsPredicate.MobEffectInstancePredicate> EffectMap)
{
    //Codec mapping from mob effect reference to instance predicate, maps to vanilla CODEC
    public static readonly Codec<MobEffectsPredicate> Codec = Codecs.UnboundedMap(
        HolderSetCodecs.MobEffectRef, MobEffectInstancePredicate.Codec).ComapFlatMap(
        map => DataResult<MobEffectsPredicate>.Success(new MobEffectsPredicate(map)),
        predicate => predicate.EffectMap);

    //Matches every expected effect must exist and its instance must satisfy the predicate, maps to vanilla matches
    public bool Matches(IReadOnlyDictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> effects)
    {
        foreach (var entry in EffectMap)
        {
            effects.TryGetValue(entry.Key, out var instance);
            if (!entry.Value.Matches(instance)) return false;
        }
        return true;
    }

    //MobEffectInstancePredicate single effect instance predicate, level and duration ranges plus ambient and visible flags
    public sealed record MobEffectInstancePredicate(
        MinMaxBounds.Ints Amplifier,
        MinMaxBounds.Ints Duration,
        Optional<bool> Ambient,
        Optional<bool> Visible)
    {
        //Codec persistence codec, field names amplifier duration ambient visible, maps to vanilla CODEC
        public static readonly Codec<MobEffectInstancePredicate> Codec = RecordCodecBuilder.Of4(
            MinMaxBounds.Ints.CODEC.OptionalFieldOf("amplifier", MinMaxBounds.Ints.Any)
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Amplifier),
            MinMaxBounds.Ints.CODEC.OptionalFieldOf("duration", MinMaxBounds.Ints.Any)
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Duration),
            Codecs.Bool.OptionalFieldOf("ambient")
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Ambient),
            Codecs.Bool.OptionalFieldOf("visible")
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Visible),
            (amplifier, duration, ambient, visible)
                => new MobEffectInstancePredicate(amplifier, duration, ambient, visible));

        //Matches a missing instance never matches; only items with an expectation are constrained, maps to vanilla matches
        public bool Matches(MobEffectInstance? instance)
            => instance is not null
                && Amplifier.Matches(instance.Amplifier)
                && Duration.Matches(instance.Duration)
                && (!Ambient.IsPresent || Ambient.Get() == instance.IsAmbient)
                && (!Visible.IsPresent || Visible.Get() == instance.IsVisible);
    }
}
