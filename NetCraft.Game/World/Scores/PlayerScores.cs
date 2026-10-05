namespace NetCraft.Game.World.Scores;

//PlayerScores 一个记分对象在各目标下的分数 对应原版 net.minecraft.world.scores.PlayerScores
//计分板内部按持有者名索引它 不对外暴露
internal sealed class PlayerScores
{
    private readonly Dictionary<Objective, Score> _scores = new();

    //Get 取指定目标的分数 没有给 null 对应原版 get
    public Score? Get(Objective objective) => _scores.TryGetValue(objective, out var score) ? score : null;

    //GetOrCreate 取或新建分数 新建时交给回调初始化 对应原版 getOrCreate
    public Score GetOrCreate(Objective objective, Action<Score>? initializer = null)
    {
        if (_scores.TryGetValue(objective, out var existing)) return existing;
        var score = new Score();
        initializer?.Invoke(score);
        _scores[objective] = score;
        return score;
    }

    //Remove 移除指定目标的分数 对应原版 remove
    public bool Remove(Objective objective) => _scores.Remove(objective);

    //HasScores 是否还有分数 对应原版 hasScores
    public bool HasScores() => _scores.Count > 0;

    //SetScore 直接放入一条分数 对应原版 setScore
    public void SetScore(Objective objective, Score score) => _scores[objective] = score;

    //RawScores 全部分数 对应原版 listRawScores
    public IReadOnlyDictionary<Objective, Score> RawScores => _scores;
}
