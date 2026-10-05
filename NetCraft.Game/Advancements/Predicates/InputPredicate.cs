using NetCraft.Codec;
using NetCraft.Game.World.Entity;

namespace NetCraft.Game.Advancements.Predicates;

//InputPredicate 按键谓词 逐项判定玩家按键状态
//对应原版 net.minecraft.advancements.predicates.InputPredicate
public sealed record InputPredicate(
    Optional<bool> Forward,
    Optional<bool> Backward,
    Optional<bool> Left,
    Optional<bool> Right,
    Optional<bool> Jump,
    Optional<bool> Sneak,
    Optional<bool> Sprint)
{
    //Codec 持久化编解码 字段名 forward backward left right jump sneak sprint 对应原版 CODEC
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

    //Matches 逐项判定 未给出期望的项不约束 对应原版 matches
    public bool Matches(Input input)
        => MatchesFlag(Forward, input.Forward)
            && MatchesFlag(Backward, input.Backward)
            && MatchesFlag(Left, input.Left)
            && MatchesFlag(Right, input.Right)
            && MatchesFlag(Jump, input.Jump)
            && MatchesFlag(Sneak, input.Shift)
            && MatchesFlag(Sprint, input.Sprint);

    //MatchesFlag 期望为空即放行 否则要求相等
    private static bool MatchesFlag(Optional<bool> match, bool value)
        => !match.IsPresent || match.Get() == value;
}
