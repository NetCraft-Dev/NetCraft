using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Scoreboard 计分板 对应原版 net.minecraft.world.scores.Scoreboard
//持目标 分数 队伍 显示槽四组容器 每处改动都经 onXxx 回调外发 子类在那里接网络同步
public class Scoreboard
{
    //HiddenScorePrefix 隐藏条目前缀 对应原版 HIDDEN_SCORE_PREFIX
    public const string HiddenScorePrefix = "#";

    private readonly Dictionary<string, Objective> _objectivesByName = new();
    private readonly Dictionary<ObjectiveCriteria, List<Objective>> _objectivesByCriteria = new();
    private readonly Dictionary<string, PlayerScores> _playerScores = new();
    private readonly Dictionary<DisplaySlot, Objective> _displayObjectives = new();
    private readonly Dictionary<string, PlayerTeam> _teamsByName = new();
    private readonly Dictionary<string, PlayerTeam> _teamsByPlayer = new();

    //GetObjective 按名字取目标 对应原版 getObjective
    public Objective? GetObjective(string? name)
        => name is not null && _objectivesByName.TryGetValue(name, out var objective) ? objective : null;

    //AddObjective 新建目标 重名报错 对应原版 addObjective
    public Objective AddObjective(string name, ObjectiveCriteria criteria, Component displayName,
        ObjectiveCriteria.RenderType renderType, bool displayAutoUpdate)
    {
        if (_objectivesByName.ContainsKey(name))
            throw new ArgumentException($"名为 {name} 的目标已存在");
        var objective = new Objective(this, name, criteria, displayName, renderType, displayAutoUpdate);
        if (!_objectivesByCriteria.TryGetValue(criteria, out var list))
        {
            list = new List<Objective>();
            _objectivesByCriteria[criteria] = list;
        }
        list.Add(objective);
        _objectivesByName[name] = objective;
        OnObjectiveAdded(objective);
        return objective;
    }

    //ForAllObjectives 对某标准下的每个目标执行操作 对应原版 forAllObjectives
    public void ForAllObjectives(ObjectiveCriteria criteria, ScoreHolder holder, Action<ScoreAccess> operation)
    {
        if (!_objectivesByCriteria.TryGetValue(criteria, out var list)) return;
        foreach (var objective in list) operation(GetOrCreatePlayerScore(holder, objective, true));
    }

    //GetOrCreatePlayerInfo 取或新建某持有者的分数表 对应原版 getOrCreatePlayerInfo
    private PlayerScores GetOrCreatePlayerInfo(string name)
    {
        if (_playerScores.TryGetValue(name, out var scores)) return scores;
        scores = new PlayerScores();
        _playerScores[name] = scores;
        return scores;
    }

    //GetOrCreatePlayerScore 取可写分数视图 只读标准下拿到的视图会挡住写 对应原版 getOrCreatePlayerScore
    public ScoreAccess GetOrCreatePlayerScore(ScoreHolder holder, Objective objective)
        => GetOrCreatePlayerScore(holder, objective, false);

    public ScoreAccess GetOrCreatePlayerScore(ScoreHolder holder, Objective objective, bool forceWritable)
    {
        var canModify = forceWritable || !objective.Criteria.IsReadOnly;
        var scores = GetOrCreatePlayerInfo(holder.GetScoreboardName());
        var isNew = scores.Get(objective) is null;
        var score = scores.GetOrCreate(objective);
        return new ScoreAccessImpl(this, holder, objective, score, canModify, isNew);
    }

    //GetPlayerScoreInfo 只读地取一条分数 对应原版 getPlayerScoreInfo
    public ReadOnlyScoreInfo? GetPlayerScoreInfo(ScoreHolder holder, Objective objective)
        => _playerScores.TryGetValue(holder.GetScoreboardName(), out var scores) ? scores.Get(objective) : null;

    //ListPlayerScores 某目标下的全部条目 对应原版 listPlayerScores
    public List<PlayerScoreEntry> ListPlayerScores(Objective objective)
    {
        var result = new List<PlayerScoreEntry>();
        foreach (var (owner, scores) in _playerScores)
            if (scores.Get(objective) is { } score)
                result.Add(new PlayerScoreEntry(owner, score.Value(), score.Display()));
        return result;
    }

    //GetObjectives 全部目标 对应原版 getObjectives
    public IReadOnlyCollection<Objective> GetObjectives() => _objectivesByName.Values;

    //GetObjectiveNames 全部目标名 对应原版 getObjectiveNames
    public IReadOnlyCollection<string> GetObjectiveNames() => _objectivesByName.Keys;

    //GetTrackedPlayers 已登记分数的持有者 对应原版 getTrackedPlayers
    public List<ScoreHolder> GetTrackedPlayers()
        => _playerScores.Keys.Select(ScoreHolder.ForNameOnly).ToList();

    //ResetAllPlayerScores 清掉某持有者的全部分数 对应原版 resetAllPlayerScores
    public void ResetAllPlayerScores(ScoreHolder holder)
    {
        if (_playerScores.Remove(holder.GetScoreboardName())) OnPlayerRemoved(holder);
    }

    //ResetSinglePlayerScore 清掉某持有者在某目标下的分数 对应原版 resetSinglePlayerScore
    public void ResetSinglePlayerScore(ScoreHolder holder, Objective objective)
    {
        if (!_playerScores.TryGetValue(holder.GetScoreboardName(), out var scores)) return;
        var removed = scores.Remove(objective);
        if (!scores.HasScores())
        {
            if (_playerScores.Remove(holder.GetScoreboardName())) OnPlayerRemoved(holder);
        }
        else if (removed)
        {
            OnPlayerScoreRemoved(holder, objective);
        }
    }

    //ListPlayerScores 某持有者在各目标下的分数 对应原版 listPlayerScores(ScoreHolder)
    public Dictionary<Objective, int> ListPlayerScores(ScoreHolder holder)
    {
        var result = new Dictionary<Objective, int>();
        if (!_playerScores.TryGetValue(holder.GetScoreboardName(), out var scores)) return result;
        foreach (var (objective, score) in scores.RawScores) result[objective] = score.Value();
        return result;
    }

    //RemoveObjective 移除目标并清显示槽与该目标的全部分数 对应原版 removeObjective
    public void RemoveObjective(Objective objective)
    {
        _objectivesByName.Remove(objective.Name);
        foreach (var slot in Enum.GetValues<DisplaySlot>())
            if (ReferenceEquals(GetDisplayObjective(slot), objective))
                SetDisplayObjective(slot, null);
        if (_objectivesByCriteria.TryGetValue(objective.Criteria, out var list)) list.Remove(objective);
        foreach (var scores in _playerScores.Values) scores.Remove(objective);
        OnObjectiveRemoved(objective);
    }

    //SetDisplayObjective 给显示槽挂目标 给 null 表示清空 对应原版 setDisplayObjective
    public void SetDisplayObjective(DisplaySlot slot, Objective? objective)
    {
        if (objective is null) _displayObjectives.Remove(slot);
        else _displayObjectives[slot] = objective;
    }

    //GetDisplayObjective 取显示槽挂的目标 对应原版 getDisplayObjective
    public Objective? GetDisplayObjective(DisplaySlot slot)
        => _displayObjectives.TryGetValue(slot, out var objective) ? objective : null;

    //GetPlayerTeam 按名字取队伍 对应原版 getPlayerTeam
    public PlayerTeam? GetPlayerTeam(string name)
        => _teamsByName.TryGetValue(name, out var team) ? team : null;

    //AddPlayerTeam 新建队伍 已存在直接返回 对应原版 addPlayerTeam
    public PlayerTeam AddPlayerTeam(string name)
    {
        if (GetPlayerTeam(name) is { } existing) return existing;
        var team = new PlayerTeam(this, name);
        _teamsByName[name] = team;
        OnTeamAdded(team);
        return team;
    }

    //RemovePlayerTeam 移除队伍并把成员从反查表摘掉 对应原版 removePlayerTeam
    public void RemovePlayerTeam(PlayerTeam team)
    {
        _teamsByName.Remove(team.GetName());
        foreach (var player in team.GetPlayers()) _teamsByPlayer.Remove(player);
        OnTeamRemoved(team);
    }

    //AddPlayerToTeam 把玩家加进队伍 已在别的队先退队 对应原版 addPlayerToTeam
    public bool AddPlayerToTeam(string player, PlayerTeam team)
    {
        if (GetPlayersTeam(player) is not null) RemovePlayerFromTeam(player);
        _teamsByPlayer[player] = team;
        return team.AddPlayer(player);
    }

    //RemovePlayerFromTeam 把玩家从它当前队伍移出 对应原版 removePlayerFromTeam
    public bool RemovePlayerFromTeam(string player)
    {
        if (GetPlayersTeam(player) is not { } team) return false;
        RemovePlayerFromTeam(player, team);
        return true;
    }

    //RemovePlayerFromTeam 从指定队伍移出 不在该队时报错 对应原版同名重载
    public void RemovePlayerFromTeam(string player, PlayerTeam team)
    {
        if (!ReferenceEquals(GetPlayersTeam(player), team))
            throw new InvalidOperationException($"玩家不在此队伍 无法移出 {team.GetName()}");
        _teamsByPlayer.Remove(player);
        team.RemovePlayer(player);
    }

    //GetTeamNames 全部队伍名 对应原版 getTeamNames
    public IReadOnlyCollection<string> GetTeamNames() => _teamsByName.Keys;

    //GetPlayerTeams 全部队伍 对应原版 getPlayerTeams
    public IReadOnlyCollection<PlayerTeam> GetPlayerTeams() => _teamsByName.Values;

    //GetPlayersTeam 取玩家所在队伍 对应原版 getPlayersTeam
    public PlayerTeam? GetPlayersTeam(string name)
        => _teamsByPlayer.TryGetValue(name, out var team) ? team : null;

    //以下 onXxx 是改动外发点 基类留空 服务端子类在这些点做网络同步
    public virtual void OnObjectiveAdded(Objective objective) { }

    public virtual void OnObjectiveChanged(Objective objective) { }

    public virtual void OnObjectiveRemoved(Objective objective) { }

    internal virtual void OnScoreChanged(ScoreHolder owner, Objective objective, Score score) { }

    internal virtual void OnScoreLockChanged(ScoreHolder owner, Objective objective) { }

    public virtual void OnPlayerRemoved(ScoreHolder player) { }

    public virtual void OnPlayerScoreRemoved(ScoreHolder player, Objective objective) { }

    public virtual void OnTeamAdded(PlayerTeam team) { }

    public virtual void OnTeamChanged(PlayerTeam team) { }

    public virtual void OnTeamRemoved(PlayerTeam team) { }

    //EntityRemoved 实体被移除时清分数并退队 对应原版 entityRemoved
    //本作实体还没有计分板名 名字由调用方传入
    public void EntityRemoved(string scoreboardName)
    {
        ResetAllPlayerScores(ScoreHolder.ForNameOnly(scoreboardName));
        RemovePlayerFromTeam(scoreboardName);
    }

    //PackObjectives 打包全部目标 对应原版 packObjectives
    public List<Objective.Packed> PackObjectives()
        => _objectivesByName.Values
            .Select(objective => new Objective.Packed(objective.Name, objective.Criteria, objective.DisplayName,
                objective.RenderType, objective.DisplayAutoUpdate))
            .ToList();

    //LoadObjective 还原一个目标 加载期不触发 onObjectiveAdded 对应原版 loadObjective
    public void LoadObjective(Objective.Packed packed)
    {
        var objective = new Objective(this, packed.Name, packed.Criteria, packed.DisplayName, packed.RenderType,
            packed.DisplayAutoUpdate);
        if (!_objectivesByCriteria.TryGetValue(packed.Criteria, out var list))
        {
            list = new List<Objective>();
            _objectivesByCriteria[packed.Criteria] = list;
        }
        list.Add(objective);
        _objectivesByName[packed.Name] = objective;
    }

    //PackPlayerScores 打包全部玩家分数 对应原版 packPlayerScores
    public List<PackedScore> PackPlayerScores()
    {
        var result = new List<PackedScore>();
        foreach (var (owner, scores) in _playerScores)
            foreach (var (objective, score) in scores.RawScores)
                result.Add(new PackedScore(owner, objective.Name,
                    new Score.Packed(score.Value(), score.IsLocked(), Optional<Component>.OfNullable(score.Display()))));
        return result;
    }

    //LoadPlayerScore 还原一条玩家分数 对应原版 loadPlayerScore
    public void LoadPlayerScore(PackedScore packed)
    {
        if (GetObjective(packed.Objective) is not { } objective) return;
        var score = GetOrCreatePlayerInfo(packed.Owner).GetOrCreate(objective);
        score.SetValue(packed.ScoreData.Value);
        score.SetLocked(packed.ScoreData.Locked);
        score.SetDisplay(packed.ScoreData.Display.IsPresent ? packed.ScoreData.Display.Get() : null);
    }

    //PackPlayerTeams 打包全部队伍 对应原版 packPlayerTeams
    public List<PlayerTeam.Packed> PackPlayerTeams()
        => _teamsByName.Values
            .Select(team => new PlayerTeam.Packed(team.GetName(), Optional<Component>.Of(team.DisplayName),
                team.GetColor() is { } color ? Optional<TeamColor>.Of(color) : Optional<TeamColor>.Empty(),
                team.IsAllowFriendlyFire(),
                team.CanSeeFriendlyInvisibles(), team.PlayerPrefix, team.PlayerSuffix, team.GetNameTagVisibility(),
                team.GetDeathMessageVisibility(), team.GetCollisionRule(), team.GetPlayers().ToList()))
            .ToList();

    //LoadPlayerTeam 还原一支队伍 加载走 setter 会触发 onTeamChanged 但加载期没接同步故无副作用
    //对应原版 loadPlayerTeam
    public void LoadPlayerTeam(PlayerTeam.Packed packed)
    {
        var team = new PlayerTeam(this, packed.Name);
        if (packed.DisplayName.IsPresent) team.SetDisplayName(packed.DisplayName.Get());
        if (packed.Color.IsPresent) team.SetColor(packed.Color.Get());
        team.SetAllowFriendlyFire(packed.AllowFriendlyFire);
        team.SetSeeFriendlyInvisibles(packed.SeeFriendlyInvisibles);
        team.SetPlayerPrefix(packed.MemberNamePrefix);
        team.SetPlayerSuffix(packed.MemberNameSuffix);
        team.SetNameTagVisibility(packed.NameTagVisibility);
        team.SetDeathMessageVisibility(packed.DeathMessageVisibility);
        team.SetCollisionRule(packed.CollisionRule);
        _teamsByName[team.GetName()] = team;
        foreach (var player in packed.Players)
        {
            _teamsByPlayer[player] = team;
            team.AddPlayer(player);
        }
    }

    //PackDisplaySlots 打包显示槽映射 对应原版 packDisplaySlots
    public Dictionary<DisplaySlot, string> PackDisplaySlots()
    {
        var result = new Dictionary<DisplaySlot, string>();
        foreach (var (slot, objective) in _displayObjectives) result[slot] = objective.Name;
        return result;
    }

    //PackedScore 单条玩家分数的存档形态 对应原版 Scoreboard.PackedScore
    public sealed record PackedScore(string Owner, string Objective, Score.Packed ScoreData)
    {
        //Codec 持久化编解码 字段名 Name/Objective/Score 对应原版 CODEC
        //原版把分数 map 直接并入本层 本作编码体系不支持 map 合并 改成嵌套字段
        public static readonly Codec<PackedScore> Codec = RecordCodecBuilder.Of3(
            Codecs.String.FieldOf("Name").ForGetter((PackedScore packed) => packed.Owner),
            Codecs.String.FieldOf("Objective").ForGetter((PackedScore packed) => packed.Objective),
            Score.Packed.Codec.FieldOf("Score").ForGetter((PackedScore packed) => packed.ScoreData),
            (owner, objective, scoreData) => new PackedScore(owner, objective, scoreData));
    }
}

//ScoreAccessImpl 计分板给出的可写分数视图 对应原版 getOrCreatePlayerScore 里的匿名实现
internal sealed class ScoreAccessImpl(Scoreboard scoreboard, ScoreHolder holder, Objective objective, Score score,
    bool canModify, bool requiresSync) : ScoreAccess
{
    private bool _requiresSync = requiresSync;

    public int Get() => score.Value();

    public void Set(int value)
    {
        if (!canModify) throw new InvalidOperationException("只读计分标准不允许改分");
        var changed = _requiresSync;
        if (objective.DisplayAutoUpdate && holder.GetDisplayName() is { } display
            && !Equals(display, score.Display()))
        {
            score.SetDisplay(display);
            changed = true;
        }
        if (value != score.Value())
        {
            score.SetValue(value);
            changed = true;
        }
        if (changed) SendScoreToPlayers();
    }

    public Component? Display() => score.Display();

    public void Display(Component? display)
    {
        if (_requiresSync || !Equals(display, score.Display()))
        {
            score.SetDisplay(display);
            SendScoreToPlayers();
        }
    }

    public bool Locked() => score.IsLocked();

    public void Unlock() => SetLocked(false);

    public void Lock() => SetLocked(true);

    private void SetLocked(bool locked)
    {
        score.SetLocked(locked);
        if (_requiresSync) SendScoreToPlayers();
        scoreboard.OnScoreLockChanged(holder, objective);
    }

    private void SendScoreToPlayers()
    {
        scoreboard.OnScoreChanged(holder, objective, score);
        _requiresSync = false;
    }
}
