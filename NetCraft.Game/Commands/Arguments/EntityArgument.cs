using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntityArgument 实体参数对应原版 net.minecraft.commands.arguments.EntityArgument
//四工厂entity/entities/player/players覆盖单多与实体玩家两维度
//执行期把解析出的选择器按命令源解析成目标集合
public sealed class EntityArgument(bool single, bool playersOnly) : ArgumentType<EntitySelector>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[]
        { "Player", "0123", "@e", "@e[type=foo]", "dd12be42-52a9-4a91-a8a1-11c01849e498" };

    //六个选择器前缀 补全时按已输入前缀过滤
    private static readonly string[] SelectorPrefixes = { "@a", "@e", "@n", "@p", "@r", "@s" };

    public static readonly SimpleCommandExceptionType ErrorNotSingleEntity =
        new(new TranslatableMessage("argument.entity.toomany"));
    public static readonly SimpleCommandExceptionType ErrorNotSinglePlayer =
        new(new TranslatableMessage("argument.player.toomany"));
    public static readonly SimpleCommandExceptionType ErrorOnlyPlayersAllowed =
        new(new TranslatableMessage("argument.player.entities"));
    public static readonly SimpleCommandExceptionType NoEntitiesFound =
        new(new TranslatableMessage("argument.entity.notfound.entity"));
    public static readonly SimpleCommandExceptionType NoPlayersFound =
        new(new TranslatableMessage("argument.entity.notfound.player"));

    //Entity 单实体参数
    public static EntityArgument Entity() => new(true, false);

    //Entities 多实体参数
    public static EntityArgument Entities() => new(false, false);

    //Player 单玩家参数
    public static EntityArgument Player() => new(true, true);

    //Players 多玩家参数
    public static EntityArgument Players() => new(false, true);

    //Single 是否单目标 网络序列化用
    public bool Single { get; } = single;

    //PlayersOnly 是否仅玩家目标 网络序列化用
    public bool PlayersOnly { get; } = playersOnly;

    //Parse 解析选择器并按参数维度校验单多与玩家限定 不满足回滚到参数起点
    public EntitySelector Parse(StringReader reader)
    {
        var start = reader.Cursor;
        //玩家命令源权限恒放行选择器
        var parser = new EntitySelectorParser(reader, allowSelectors: true);
        var selector = parser.Parse();
        if (selector.MaxResults > 1 && single)
        {
            reader.SetCursor(start);
            throw (playersOnly ? ErrorNotSinglePlayer : ErrorNotSingleEntity).CreateWithContext(reader);
        }
        if (selector.IncludesEntities && playersOnly && !selector.IsSelfSelector)
        {
            reader.SetCursor(start);
            throw ErrorOnlyPlayersAllowed.CreateWithContext(reader);
        }
        return selector;
    }

    //GetEntity 取单实体目标 本作 tp 只支持玩家 命中非玩家实体按原版只允许玩家的报错处理
    public static ServerPlayer GetEntity(CommandContext<CommandSourceStack> context, string name)
    {
        var target = context.GetArgument<EntitySelector>(name).FindSingleEntity(GetSource(context));
        return target.Player ?? throw ErrorOnlyPlayersAllowed.Create();
    }

    //GetSingleTarget 取单个实体目标 玩家与关卡实体都可能返回 对应原版 EntityArgument.getEntity
    //既有 GetEntity 只给玩家 /data 这类要操作任意实体的命令走这条
    public static CommandTarget GetSingleTarget(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindSingleEntity(GetSource(context));

    //GetEntities 取多实体目标 含玩家与关卡实体 空结果抛NO_ENTITIES_FOUND
    public static IReadOnlyList<CommandTarget> GetEntities(CommandContext<CommandSourceStack> context, string name)
    {
        var result = GetOptionalEntities(context, name);
        if (result.Count == 0)
            throw NoEntitiesFound.Create();
        return result;
    }

    //GetOptionalEntities 取多实体目标 允许空结果
    public static IReadOnlyList<CommandTarget> GetOptionalEntities(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindEntities(GetSource(context));

    //GetPlayer 取单玩家目标
    public static ServerPlayer GetPlayer(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindSinglePlayer(GetSource(context));

    //GetPlayers 取多玩家目标 空结果抛NO_PLAYERS_FOUND
    public static IReadOnlyList<ServerPlayer> GetPlayers(CommandContext<CommandSourceStack> context, string name)
    {
        var players = context.GetArgument<EntitySelector>(name).FindPlayers(GetSource(context));
        if (players.Count == 0)
            throw NoPlayersFound.Create();
        return players;
    }

    //GetOptionalPlayers 取多玩家目标 允许空结果
    public static IReadOnlyList<ServerPlayer> GetOptionalPlayers(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<EntitySelector>(name).FindPlayers(GetSource(context));

    private static ServerCommandSource GetSource(CommandContext<CommandSourceStack> context)
        => (ServerCommandSource)context.GetSource();

    //ListSuggestions 补全选择器类型前缀与在线玩家名 对应原版 EntitySelectorParser 的建议
    //空输入与 @ 开头都给选择器类型 前者额外再给在线玩家名
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining;
        if (remaining.Length == 0 || remaining.StartsWith('@'))
        {
            foreach (var prefix in SelectorPrefixes)
                if (prefix.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(prefix);
            if (remaining.Length > 0) return builder.BuildFuture();
        }
        if (context.GetSource() is ServerCommandSource source)
        {
            foreach (var player in source.Server.PlayerList.Players)
                if (player.Profile.Name.StartsWith(remaining, StringComparison.OrdinalIgnoreCase))
                    builder.Add(player.Profile.Name);
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
