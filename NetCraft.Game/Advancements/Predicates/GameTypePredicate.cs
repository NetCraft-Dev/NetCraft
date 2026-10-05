using NetCraft.Codec;
using NetCraft.Game.World.Level;

namespace NetCraft.Game.Advancements.Predicates;

//GameTypePredicate 游戏模式谓词 判定模式是否落在给定集合内
//对应原版 net.minecraft.advancements.predicates.GameTypePredicate
public sealed record GameTypePredicate(IReadOnlyList<GameType> Types)
{
    //Any 全部模式 对应原版 ANY
    public static readonly GameTypePredicate Any =
        Of(GameType.Survival, GameType.Creative, GameType.Adventure, GameType.Spectator);

    //SurvivalLike 生存与冒险 对应原版 SURVIVAL_LIKE
    public static readonly GameTypePredicate SurvivalLike = Of(GameType.Survival, GameType.Adventure);

    //Codec 模式名字列表 对应原版 CODEC
    public static readonly Codec<GameTypePredicate> Codec = GameType.Codec.ListOf().ComapFlatMap(
        types => DataResult<GameTypePredicate>.Success(new GameTypePredicate(types)),
        predicate => predicate.Types);

    //Of 按给定模式构造 对应原版 of
    public static GameTypePredicate Of(params GameType[] types) => new(types);

    //Matches 模式是否在集合内 对应原版 matches
    public bool Matches(GameType type) => Types.Contains(type);
}
