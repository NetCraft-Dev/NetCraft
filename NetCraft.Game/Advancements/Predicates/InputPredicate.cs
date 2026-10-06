using NetCraft.Codec;
using NetCraft.Game.World.Entity;

namespace NetCraft.Game.Advancements.Predicates;

//InputPredicate input predicate, checks the player's key states item by item
//maps to vanilla net.minecraft.advancements.predicates.InputPredicate
public sealed record InputPredicate(
    Optional<bool> Forward,
    Optional<bool> Backward,
    Optional<bool> Left,
    Optional<bool> Right,
    Optional<bool> Jump,
    Optional<bool> Sneak,
    Optional<bool> Sprint)
{
    //Codec persistence codec, field names forward backward left right jump sneak sprint, maps to vanilla CODEC
    public static readonly Codec<InputPredicate> Codec = RecordCodecBuilder.Of7(
        Codecs.Bool.OptionalFieldOf("forward").ForGetter((InputPredicate predicate) => predicate.Forward),
        Codecs.Bool.OptionalFieldOf("backward").ForGetter((InputPredicate predicate) => predicate.Backward),
        Codecs.Bool.OptionalFieldOf("left").ForGetter((InputPredicate predicate) => predicate.Left),
        Codecs.Bool.OptionalFieldOf("right").ForGetter((InputPredicate predicate) => predicate.Right),
        Codecs.Bool.OptionalFieldOf("jump").ForGetter((InputPredicate predicate) => predicate.Jump),
        Codecs.Bool.OptionalFieldOf("sneak").ForGetter((InputPredicate predicate) => predicate.Sneak),
        Codecs.Bool.OptionalFieldOf("sprint").ForGetter((InputPredicate predicate) => predicate.Sprint),
        (forward, backward, left, right, jump, sneak, sprint)
            => new InputPredicate(forward, backward, left, right, jump, sneak, sprint));

    //Matches checked item by item; entries without an expectation are unconstrained, maps to vanilla matches
    public bool Matches(Input input)
        => MatchesFlag(Forward, input.Forward)
            && MatchesFlag(Backward, input.Backward)
            && MatchesFlag(Left, input.Left)
            && MatchesFlag(Right, input.Right)
            && MatchesFlag(Jump, input.Jump)
            && MatchesFlag(Sneak, input.Shift)
            && MatchesFlag(Sprint, input.Sprint);

    //MatchesFlag an empty expectation passes, otherwise equality is required
    private static bool MatchesFlag(Optional<bool> match, bool value)
        => !match.IsPresent || match.Get() == value;
}
