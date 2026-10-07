using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Arguments;

//EntitySelector entity selector, maps to vanilla net.minecraft.commands.arguments.selector.EntitySelector
//Holds the filters assembled at parse time and resolves the target set against the command source at execution time
//Two target sources: online players and level entities, both wrapped as CommandTarget through the same filtering and sorting
public sealed class EntitySelector
{
    public const int Infinite = int.MaxValue;

    //Player hit box 0.6 wide, 1.8 tall, for aabb intersection filtering
    public const float PlayerWidth = 0.6f;
    public const float PlayerHeight = 1.8f;

    //The sorter sorts the candidate list by a reference point, maps to vanilla BiConsumer<Vec3,List>
    public delegate void Orderer(Vec3 pos, List<CommandTarget> list);

    //OrderArbitrary does not sort, keeping natural order
    public static readonly Orderer OrderArbitrary = (_, _) => { };

    private readonly int _maxResults;
    private readonly bool _includesEntities;
    private readonly bool _worldLimited;
    private readonly IReadOnlyList<Predicate<CommandTarget>> _contextFreePredicates;
    private readonly MinMaxBounds.Doubles? _range;
    private readonly Func<Vec3, Vec3> _position;
    private readonly AABB? _aabb;
    private readonly Orderer _order;
    private readonly bool _currentEntity;
    private readonly string? _playerName;
    private readonly Guid? _entityUuid;
    private readonly EntityType<object>? _type;
    private readonly bool _usesSelector;

    public EntitySelector(int maxResults, bool includesEntities, bool worldLimited,
        IReadOnlyList<Predicate<CommandTarget>> contextFreePredicates, MinMaxBounds.Doubles? range,
        Func<Vec3, Vec3> position, AABB? aabb, Orderer order, bool currentEntity,
        string? playerName, Guid? entityUuid, EntityType<object>? type, bool usesSelector)
    {
        _maxResults = maxResults;
        _includesEntities = includesEntities;
        _worldLimited = worldLimited;
        _contextFreePredicates = contextFreePredicates;
        _range = range;
        _position = position;
        _aabb = aabb;
        _order = order;
        _currentEntity = currentEntity;
        _playerName = playerName;
        _entityUuid = entityUuid;
        _type = type;
        _usesSelector = usesSelector;
    }

    public int MaxResults => _maxResults;
    public bool IncludesEntities => _includesEntities;
    public bool IsSelfSelector => _currentEntity;
    public bool IsWorldLimited => _worldLimited;
    public bool UsesSelector => _usesSelector;

    //Type the type filter has already become a predicate at parse time; the parse result is kept here for diagnostics
    public EntityType<object>? Type => _type;

    //FindSingleEntity: an empty result throws NO_ENTITIES_FOUND, more than one throws ERROR_NOT_SINGLE_ENTITY
    public CommandTarget FindSingleEntity(ServerCommandSource source)
    {
        var entities = FindEntities(source);
        if (entities.Count == 0)
            throw EntityArgument.NoEntitiesFound.Create();
        if (entities.Count > 1)
            throw EntityArgument.ErrorNotSingleEntity.Create();
        return entities[0];
    }

    //FindEntities multiple entity result; name/UUID first, then aabb and predicate filtering, finally sorted and truncated
    public List<CommandTarget> FindEntities(ServerCommandSource source)
    {
        //Selectors such as @a/@p/@r only act on players
        if (!_includesEntities)
            return FindPlayers(source).Select(CommandTarget.OfPlayer).ToList();
        if (_playerName is not null)
        {
            var named = FindByName(source, _playerName);
            return named is null
                ? new List<CommandTarget>()
                : new List<CommandTarget> { CommandTarget.OfPlayer(named) };
        }
        if (_entityUuid is not null)
        {
            var player = FindByUuid(source, _entityUuid.Value);
            if (player is not null) return new List<CommandTarget> { CommandTarget.OfPlayer(player) };
            var entity = source.Server.Overworld.EntityManager.GetByUuid(_entityUuid.Value);
            return entity is null
                ? new List<CommandTarget>()
                : new List<CommandTarget> { CommandTarget.OfEntity(entity) };
        }
        var pos = _position(source.Position);
        var absoluteAabb = GetAbsoluteAabb(pos);
        if (_currentEntity)
        {
            var self = CommandTarget.OfPlayer(source.PlayerOrThrow);
            var selfPredicate = GetPredicate(pos, absoluteAabb);
            return selfPredicate(self) ? new List<CommandTarget> { self } : new List<CommandTarget>();
        }
        var predicate = GetPredicate(pos, absoluteAabb);
        var result = new List<CommandTarget>();
        AddEntities(result, source, predicate);
        return SortAndLimit(pos, result);
    }

    //FindSinglePlayer single player result; throws NO_PLAYERS_FOUND when the count is not 1
    public ServerPlayer FindSinglePlayer(ServerCommandSource source)
    {
        var players = FindPlayers(source);
        if (players.Count != 1)
            throw EntityArgument.NoPlayersFound.Create();
        return players[0];
    }

    //FindPlayers multiple player result; iterates online players only, wrapping the predicate in the player view first
    public List<ServerPlayer> FindPlayers(ServerCommandSource source)
    {
        if (_playerName is not null)
        {
            var named = FindByName(source, _playerName);
            return named is null ? new List<ServerPlayer>() : new List<ServerPlayer> { named };
        }
        if (_entityUuid is not null)
        {
            var matched = FindByUuid(source, _entityUuid.Value);
            return matched is null ? new List<ServerPlayer>() : new List<ServerPlayer> { matched };
        }
        var pos = _position(source.Position);
        var absoluteAabb = GetAbsoluteAabb(pos);
        var predicate = GetPlayerPredicate(pos, absoluteAabb);
        if (_currentEntity)
        {
            var self = source.PlayerOrThrow;
            return predicate(self) ? new List<ServerPlayer> { self } : new List<ServerPlayer>();
        }
        var limit = GetResultLimit();
        var result = new List<ServerPlayer>();
        foreach (var player in source.Server.PlayerList.Players)
        {
            if (predicate(player))
            {
                result.Add(player);
                if (result.Count >= limit)
                    return result;
            }
        }
        return SortAndLimitPlayers(pos, result);
    }

    //FindByName looks up by player name, case-insensitive like vanilla getPlayerByName
    private static ServerPlayer? FindByName(ServerCommandSource source, string name)
        => source.Server.PlayerList.Players.FirstOrDefault(
            p => string.Equals(p.Profile.Name, name, StringComparison.OrdinalIgnoreCase));

    //FindByUuid looks up by UUID
    private static ServerPlayer? FindByUuid(ServerCommandSource source, Guid uuid)
        => source.Server.PlayerList.Players.FirstOrDefault(p => p.Uuid == uuid);

    //AddEntities collects players and level entities together and exits early once the result limit is reached
    private void AddEntities(List<CommandTarget> result, ServerCommandSource source,
        Predicate<CommandTarget> predicate)
    {
        var limit = GetResultLimit();
        if (result.Count >= limit) return;
        foreach (var player in source.Server.PlayerList.Players)
        {
            if (result.Count >= limit) return;
            var target = CommandTarget.OfPlayer(player);
            if (predicate(target))
                result.Add(target);
        }
        //Level entities are materialized first, because command execution may remove entities and iterating the live set would hit modification
        foreach (var entity in source.Server.Overworld.Entities.ToList())
        {
            if (result.Count >= limit) return;
            var target = CommandTarget.OfEntity(entity);
            if (predicate(target))
                result.Add(target);
        }
    }

    //GetResultLimit collects everything before truncating when sorting is present; returns unbounded for non-ARBITRARY
    private int GetResultLimit()
        => _order == OrderArbitrary ? _maxResults : Infinite;

    //GetAbsoluteAabb translates the relative aabb to the reference point
    private AABB? GetAbsoluteAabb(Vec3 pos)
        => _aabb?.Move(pos);

    //GetPredicate assembles the context-dependent filters: aabb intersection and squared distance
    private Predicate<CommandTarget> GetPredicate(Vec3 pos, AABB? absoluteAabb)
    {
        var predicates = new List<Predicate<CommandTarget>>(_contextFreePredicates);
        if (absoluteAabb is not null)
        {
            var aabb = absoluteAabb.Value;
            predicates.Add(e => aabb.Intersects(e.BoundingBox));
        }
        if (_range is not null)
            predicates.Add(e => _range.MatchesSqr(e.Position.DistanceToSqr(pos)));
        return e => predicates.All(p => p(e));
    }

    //GetPlayerPredicate narrows the target predicate to a player predicate
    private Predicate<ServerPlayer> GetPlayerPredicate(Vec3 pos, AABB? absoluteAabb)
    {
        var predicate = GetPredicate(pos, absoluteAabb);
        return player => predicate(CommandTarget.OfPlayer(player));
    }

    //GetBoundingBox the player hit box is centered on the feet
    public static AABB GetBoundingBox(ServerPlayer player)
    {
        var pos = player.Position;
        var half = PlayerWidth / 2;
        return new AABB(pos.X - half, pos.Y, pos.Z - half,
            pos.X + half, pos.Y + PlayerHeight, pos.Z + half);
    }

    //SortAndLimit sorts multiple results then truncates by maxResults
    private List<CommandTarget> SortAndLimit(Vec3 pos, List<CommandTarget> result)
    {
        if (result.Count > 1)
            _order(pos, result);
        return result.GetRange(0, Math.Min(_maxResults, result.Count));
    }

    //SortAndLimitPlayers sorts player results, wrapping into a target list then unwrapping
    private List<ServerPlayer> SortAndLimitPlayers(Vec3 pos, List<ServerPlayer> result)
    {
        if (result.Count > 1)
        {
            var wrapped = result.Select(CommandTarget.OfPlayer).ToList();
            _order(pos, wrapped);
            result = wrapped.Select(target => target.Player!).ToList();
        }
        return result.GetRange(0, Math.Min(_maxResults, result.Count));
    }
}
