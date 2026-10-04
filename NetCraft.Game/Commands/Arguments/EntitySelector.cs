using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Arguments;

//EntitySelector 实体选择器对应原版 net.minecraft.commands.arguments.selector.EntitySelector
//持解析期组装的过滤条件在执行期按命令源解析出目标集合
//目标来源两类 在线玩家与关卡实体 都包成 CommandTarget 走同一套过滤与排序
public sealed class EntitySelector
{
    public const int Infinite = int.MaxValue;

    //玩家碰撞箱宽0.6高1.8 供aabb相交过滤
    public const float PlayerWidth = 0.6f;
    public const float PlayerHeight = 1.8f;

    //排序器按基准点对候选列表排序对应原版BiConsumer<Vec3,List>
    public delegate void Orderer(Vec3 pos, List<CommandTarget> list);

    //OrderArbitrary 不排序保留自然顺序
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

    //Type 类型过滤在解析期已转为谓词 这里保留解析结果供诊断
    public EntityType<object>? Type => _type;

    //FindSingleEntity 单实体结果空抛NO_ENTITIES_FOUND多于一个抛ERROR_NOT_SINGLE_ENTITY
    public CommandTarget FindSingleEntity(ServerCommandSource source)
    {
        var entities = FindEntities(source);
        if (entities.Count == 0)
            throw EntityArgument.NoEntitiesFound.Create();
        if (entities.Count > 1)
            throw EntityArgument.ErrorNotSingleEntity.Create();
        return entities[0];
    }

    //FindEntities 多实体结果 按名字/UUID优先其次aabb与谓词过滤最后排序截断
    public List<CommandTarget> FindEntities(ServerCommandSource source)
    {
        //@a/@p/@r 一类选择器只作用于玩家
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

    //FindSinglePlayer 单玩家结果 数量不为1抛NO_PLAYERS_FOUND
    public ServerPlayer FindSinglePlayer(ServerCommandSource source)
    {
        var players = FindPlayers(source);
        if (players.Count != 1)
            throw EntityArgument.NoPlayersFound.Create();
        return players[0];
    }

    //FindPlayers 多玩家结果 只遍历在线玩家 谓词先包成玩家视图再套用
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

    //FindByName 按玩家名查找大小写不敏感对齐原版getPlayerByName
    private static ServerPlayer? FindByName(ServerCommandSource source, string name)
        => source.Server.PlayerList.Players.FirstOrDefault(
            p => string.Equals(p.Profile.Name, name, StringComparison.OrdinalIgnoreCase));

    //FindByUuid 按UUID查找
    private static ServerPlayer? FindByUuid(ServerCommandSource source, Guid uuid)
        => source.Server.PlayerList.Players.FirstOrDefault(p => p.Uuid == uuid);

    //AddEntities 玩家与关卡实体一起收集 到达结果上限提前退出
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
        //关卡实体先物化 命令执行期会移除实体 直接遍历可见集合会踩到改动
        foreach (var entity in source.Server.Overworld.Entities.ToList())
        {
            if (result.Count >= limit) return;
            var target = CommandTarget.OfEntity(entity);
            if (predicate(target))
                result.Add(target);
        }
    }

    //GetResultLimit 排序存在时先收集全部再截断 非ARBITRARY返回无限
    private int GetResultLimit()
        => _order == OrderArbitrary ? _maxResults : Infinite;

    //GetAbsoluteAabb 相对aabb平移到基准点
    private AABB? GetAbsoluteAabb(Vec3 pos)
        => _aabb?.Move(pos);

    //GetPredicate 组装上下文相关过滤 aabb相交与距离平方
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

    //GetPlayerPredicate 把目标谓词收窄成玩家谓词
    private Predicate<ServerPlayer> GetPlayerPredicate(Vec3 pos, AABB? absoluteAabb)
    {
        var predicate = GetPredicate(pos, absoluteAabb);
        return player => predicate(CommandTarget.OfPlayer(player));
    }

    //GetBoundingBox 玩家碰撞箱以脚底为中心
    public static AABB GetBoundingBox(ServerPlayer player)
    {
        var pos = player.Position;
        var half = PlayerWidth / 2;
        return new AABB(pos.X - half, pos.Y, pos.Z - half,
            pos.X + half, pos.Y + PlayerHeight, pos.Z + half);
    }

    //SortAndLimit 多于一个结果先排序再按maxResults截断
    private List<CommandTarget> SortAndLimit(Vec3 pos, List<CommandTarget> result)
    {
        if (result.Count > 1)
            _order(pos, result);
        return result.GetRange(0, Math.Min(_maxResults, result.Count));
    }

    //SortAndLimitPlayers 玩家结果排序 包成目标列表排完再摊回玩家
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
