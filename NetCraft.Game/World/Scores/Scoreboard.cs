using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Scoreboard the scoreboard, maps to vanilla net.minecraft.world.scores.Scoreboard
//Holds four groups of containers: objectives, scores, teams and display slots; every change goes out through onXxx callbacks where subclasses hook up network sync
public class Scoreboard
{
    //HiddenScorePrefix hidden entry prefix, maps to vanilla HIDDEN_SCORE_PREFIX
    public const string HiddenScorePrefix = "#";

    private readonly Dictionary<string, Objective> _objectivesByName = new();
    private readonly Dictionary<ObjectiveCriteria, List<Objective>> _objectivesByCriteria = new();
    private readonly Dictionary<string, PlayerScores> _playerScores = new();
    private readonly Dictionary<DisplaySlot, Objective> _displayObjectives = new();
    private readonly Dictionary<string, PlayerTeam> _teamsByName = new();
    private readonly Dictionary<string, PlayerTeam> _teamsByPlayer = new();

    //GetObjective returns an objective by name, maps to vanilla getObjective
    public Objective? GetObjective(string? name)
        => name is not null && _objectivesByName.TryGetValue(name, out var objective) ? objective : null;

    //AddObjective creates an objective, errors on a duplicate name, maps to vanilla addObjective
    public Objective AddObjective(string name, ObjectiveCriteria criteria, Component displayName,
        ObjectiveCriteria.RenderType renderType, bool displayAutoUpdate)
    {
        if (_objectivesByName.ContainsKey(name))
            throw new ArgumentException($"an objective named {name} already exists");
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

    //ForAllObjectives runs an action for every objective under a criteria, maps to vanilla forAllObjectives
    public void ForAllObjectives(ObjectiveCriteria criteria, ScoreHolder holder, Action<ScoreAccess> operation)
    {
        if (!_objectivesByCriteria.TryGetValue(criteria, out var list)) return;
        foreach (var objective in list) operation(GetOrCreatePlayerScore(holder, objective, true));
    }

    //GetOrCreatePlayerInfo returns or creates the score map of a holder, maps to vanilla getOrCreatePlayerInfo
    private PlayerScores GetOrCreatePlayerInfo(string name)
    {
        if (_playerScores.TryGetValue(name, out var scores)) return scores;
        scores = new PlayerScores();
        _playerScores[name] = scores;
        return scores;
    }

    //GetOrCreatePlayerScore returns a writable score view; the one obtained under read-only criteria blocks writes, maps to vanilla getOrCreatePlayerScore
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

    //GetPlayerScoreInfo reads a score read-only, maps to vanilla getPlayerScoreInfo
    public ReadOnlyScoreInfo? GetPlayerScoreInfo(ScoreHolder holder, Objective objective)
        => _playerScores.TryGetValue(holder.GetScoreboardName(), out var scores) ? scores.Get(objective) : null;

    //ListPlayerScores all entries under an objective, maps to vanilla listPlayerScores
    public List<PlayerScoreEntry> ListPlayerScores(Objective objective)
    {
        var result = new List<PlayerScoreEntry>();
        foreach (var (owner, scores) in _playerScores)
            if (scores.Get(objective) is { } score)
                result.Add(new PlayerScoreEntry(owner, score.Value(), score.Display()));
        return result;
    }

    //GetObjectives all objectives, maps to vanilla getObjectives
    public IReadOnlyCollection<Objective> GetObjectives() => _objectivesByName.Values;

    //GetObjectiveNames all objective names, maps to vanilla getObjectiveNames
    public IReadOnlyCollection<string> GetObjectiveNames() => _objectivesByName.Keys;

    //GetTrackedPlayers holders with registered scores, maps to vanilla getTrackedPlayers
    public List<ScoreHolder> GetTrackedPlayers()
        => _playerScores.Keys.Select(ScoreHolder.ForNameOnly).ToList();

    //ResetAllPlayerScores clears all scores of a holder, maps to vanilla resetAllPlayerScores
    public void ResetAllPlayerScores(ScoreHolder holder)
    {
        if (_playerScores.Remove(holder.GetScoreboardName())) OnPlayerRemoved(holder);
    }

    //ResetSinglePlayerScore clears a holder's score under one objective, maps to vanilla resetSinglePlayerScore
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

    //ListPlayerScores a holder's scores across objectives, maps to vanilla listPlayerScores(ScoreHolder)
    public Dictionary<Objective, int> ListPlayerScores(ScoreHolder holder)
    {
        var result = new Dictionary<Objective, int>();
        if (!_playerScores.TryGetValue(holder.GetScoreboardName(), out var scores)) return result;
        foreach (var (objective, score) in scores.RawScores) result[objective] = score.Value();
        return result;
    }

    //RemoveObjective removes an objective along with its display slot and all its scores, maps to vanilla removeObjective
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

    //SetDisplayObjective attaches an objective to a display slot, null clears it, maps to vanilla setDisplayObjective
    public void SetDisplayObjective(DisplaySlot slot, Objective? objective)
    {
        if (objective is null) _displayObjectives.Remove(slot);
        else _displayObjectives[slot] = objective;
    }

    //GetDisplayObjective returns the objective on a display slot, maps to vanilla getDisplayObjective
    public Objective? GetDisplayObjective(DisplaySlot slot)
        => _displayObjectives.TryGetValue(slot, out var objective) ? objective : null;

    //GetPlayerTeam returns a team by name, maps to vanilla getPlayerTeam
    public PlayerTeam? GetPlayerTeam(string name)
        => _teamsByName.TryGetValue(name, out var team) ? team : null;

    //AddPlayerTeam creates a team, returns the existing one when present, maps to vanilla addPlayerTeam
    public PlayerTeam AddPlayerTeam(string name)
    {
        if (GetPlayerTeam(name) is { } existing) return existing;
        var team = new PlayerTeam(this, name);
        _teamsByName[name] = team;
        OnTeamAdded(team);
        return team;
    }

    //RemovePlayerTeam removes a team and detaches its members from the reverse map, maps to vanilla removePlayerTeam
    public void RemovePlayerTeam(PlayerTeam team)
    {
        _teamsByName.Remove(team.GetName());
        foreach (var player in team.GetPlayers()) _teamsByPlayer.Remove(player);
        OnTeamRemoved(team);
    }

    //AddPlayerToTeam adds a player to a team, leaving the old team first, maps to vanilla addPlayerToTeam
    public bool AddPlayerToTeam(string player, PlayerTeam team)
    {
        if (GetPlayersTeam(player) is not null) RemovePlayerFromTeam(player);
        _teamsByPlayer[player] = team;
        return team.AddPlayer(player);
    }

    //RemovePlayerFromTeam removes a player from its current team, maps to vanilla removePlayerFromTeam
    public bool RemovePlayerFromTeam(string player)
    {
        if (GetPlayersTeam(player) is not { } team) return false;
        RemovePlayerFromTeam(player, team);
        return true;
    }

    //RemovePlayerFromTeam removes from the specified team, errors when the player is not in it, maps to the vanilla overload of the same name
    public void RemovePlayerFromTeam(string player, PlayerTeam team)
    {
        if (!ReferenceEquals(GetPlayersTeam(player), team))
            throw new InvalidOperationException($"player is not in team {team.GetName()} and cannot be removed");
        _teamsByPlayer.Remove(player);
        team.RemovePlayer(player);
    }

    //GetTeamNames all team names, maps to vanilla getTeamNames
    public IReadOnlyCollection<string> GetTeamNames() => _teamsByName.Keys;

    //GetPlayerTeams all teams, maps to vanilla getPlayerTeams
    public IReadOnlyCollection<PlayerTeam> GetPlayerTeams() => _teamsByName.Values;

    //GetPlayersTeam returns the team a player belongs to, maps to vanilla getPlayersTeam
    public PlayerTeam? GetPlayersTeam(string name)
        => _teamsByPlayer.TryGetValue(name, out var team) ? team : null;

    //The onXxx methods below are the change broadcast points, empty in the base class; the server subclass does network sync there
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

    //EntityRemoved clears scores and leaves the team when an entity is removed, maps to vanilla entityRemoved
    //Entities here do not have a scoreboard name yet, the caller passes the name in
    public void EntityRemoved(string scoreboardName)
    {
        ResetAllPlayerScores(ScoreHolder.ForNameOnly(scoreboardName));
        RemovePlayerFromTeam(scoreboardName);
    }

    //PackObjectives packs all objectives, maps to vanilla packObjectives
    public List<Objective.Packed> PackObjectives()
        => _objectivesByName.Values
            .Select(objective => new Objective.Packed(objective.Name, objective.Criteria, objective.DisplayName,
                objective.RenderType, objective.DisplayAutoUpdate))
            .ToList();

    //LoadObjective restores an objective without firing onObjectiveAdded during load, maps to vanilla loadObjective
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

    //PackPlayerScores packs all player scores, maps to vanilla packPlayerScores
    public List<PackedScore> PackPlayerScores()
    {
        var result = new List<PackedScore>();
        foreach (var (owner, scores) in _playerScores)
            foreach (var (objective, score) in scores.RawScores)
                result.Add(new PackedScore(owner, objective.Name,
                    new Score.Packed(score.Value(), score.IsLocked(), Optional<Component>.OfNullable(score.Display()))));
        return result;
    }

    //LoadPlayerScore restores one player score, maps to vanilla loadPlayerScore
    public void LoadPlayerScore(PackedScore packed)
    {
        if (GetObjective(packed.Objective) is not { } objective) return;
        var score = GetOrCreatePlayerInfo(packed.Owner).GetOrCreate(objective);
        score.SetValue(packed.ScoreData.Value);
        score.SetLocked(packed.ScoreData.Locked);
        score.SetDisplay(packed.ScoreData.Display.IsPresent ? packed.ScoreData.Display.Get() : null);
    }

    //PackPlayerTeams packs all teams, maps to vanilla packPlayerTeams
    public List<PlayerTeam.Packed> PackPlayerTeams()
        => _teamsByName.Values
            .Select(team => new PlayerTeam.Packed(team.GetName(), Optional<Component>.Of(team.DisplayName),
                team.GetColor() is { } color ? Optional<TeamColor>.Of(color) : Optional<TeamColor>.Empty(),
                team.IsAllowFriendlyFire(),
                team.CanSeeFriendlyInvisibles(), team.PlayerPrefix, team.PlayerSuffix, team.GetNameTagVisibility(),
                team.GetDeathMessageVisibility(), team.GetCollisionRule(), team.GetPlayers().ToList()))
            .ToList();

    //LoadPlayerTeam restores a team; loading goes through setters which fire onTeamChanged, but no sync is hooked up during load so there is no side effect
    //Maps to vanilla loadPlayerTeam
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

    //PackDisplaySlots packs the display slot map, maps to vanilla packDisplaySlots
    public Dictionary<DisplaySlot, string> PackDisplaySlots()
    {
        var result = new Dictionary<DisplaySlot, string>();
        foreach (var (slot, objective) in _displayObjectives) result[slot] = objective.Name;
        return result;
    }

    //PackedScore save form of one player score, maps to vanilla Scoreboard.PackedScore
    public sealed record PackedScore(string Owner, string Objective, Score.Packed ScoreData)
    {
        //Codec persistence codec, field names Name/Objective/Score, maps to vanilla CODEC
        //Vanilla merges the score map directly into this level; the codec system here does not support map merging, so it uses a nested field
        public static readonly Codec<PackedScore> Codec = RecordCodecBuilder.Of3(
            Codecs.String.FieldOf("Name").ForGetter((PackedScore packed) => packed.Owner),
            Codecs.String.FieldOf("Objective").ForGetter((PackedScore packed) => packed.Objective),
            Score.Packed.Codec.FieldOf("Score").ForGetter((PackedScore packed) => packed.ScoreData),
            (owner, objective, scoreData) => new PackedScore(owner, objective, scoreData));
    }
}

//ScoreAccessImpl writable score view provided by the scoreboard, maps to the anonymous implementation inside vanilla getOrCreatePlayerScore
internal sealed class ScoreAccessImpl(Scoreboard scoreboard, ScoreHolder holder, Objective objective, Score score,
    bool canModify, bool requiresSync) : ScoreAccess
{
    private bool _requiresSync = requiresSync;

    public int Get() => score.Value();

    public void Set(int value)
    {
        if (!canModify) throw new InvalidOperationException("read-only criteria do not allow score changes");
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
