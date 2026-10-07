namespace NetCraft.Game.World.Scores;

//PlayerScores the scores of one score holder across objectives, maps to vanilla net.minecraft.world.scores.PlayerScores
//The scoreboard indexes it internally by holder name, not exposed
internal sealed class PlayerScores
{
    private readonly Dictionary<Objective, Score> _scores = new();

    //Get returns the score for an objective, null when none, maps to vanilla get
    public Score? Get(Objective objective) => _scores.TryGetValue(objective, out var score) ? score : null;

    //GetOrCreate returns or creates a score, the callback initializes it on creation, maps to vanilla getOrCreate
    public Score GetOrCreate(Objective objective, Action<Score>? initializer = null)
    {
        if (_scores.TryGetValue(objective, out var existing)) return existing;
        var score = new Score();
        initializer?.Invoke(score);
        _scores[objective] = score;
        return score;
    }

    //Remove removes the score for an objective, maps to vanilla remove
    public bool Remove(Objective objective) => _scores.Remove(objective);

    //HasScores whether any scores remain, maps to vanilla hasScores
    public bool HasScores() => _scores.Count > 0;

    //SetScore puts a score in directly, maps to vanilla setScore
    public void SetScore(Objective objective, Score score) => _scores[objective] = score;

    //RawScores all scores, maps to vanilla listRawScores
    public IReadOnlyDictionary<Objective, Score> RawScores => _scores;
}
