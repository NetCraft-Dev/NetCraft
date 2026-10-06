using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Suggestion;
using NetCraft.Commands.Tree;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Profiling;

namespace NetCraft.Game.Commands;

//CommandManager 命令管理器对应原版 net.minecraft.commands.Commands
//持命令分发器 注册内置命令 提供命令树下发与玩家命令执行入口
public sealed class CommandManager
{
    private readonly CommandDispatcher<CommandSourceStack> _dispatcher = new();
    private readonly MinecraftServer _server;

    //debugCommands 显式指定是否注册 /debug 传 null 时取 server.properties 的 nc-debug-commands
    //带这个参数是为了测试能在没有 server.properties 的场景下打开 debug 树
    public CommandManager(MinecraftServer server, bool? debugCommands = null)
    {
        _server = server;
        RegisterHelp();
        RegisterGameMode();
        RegisterTeleport();
        RegisterTime();
        RegisterGive();
        RegisterItem();
        RegisterData();
        RegisterClear();
        RegisterKill();
        RegisterSetBlock();
        RegisterFill();
        RegisterGameRule();
        RegisterWorldBorder();
        RegisterWeather();
        RegisterForceLoad();
        RegisterSpreadPlayers();
        RegisterSay();
        RegisterTellraw();
        RegisterTitle();
        RegisterStop();
        RegisterOp();
        RegisterKick();
        RegisterBan();
        RegisterListPlayers();
        RegisterEmote();
        RegisterMsg();
        RegisterSeed();
        RegisterVersion();
        RegisterSwing();
        RegisterStopwatch();
        //server 为 null 只出现在测试里直接取命令树的构造方式 这时按打开处理 生产一律看 server.properties
        RegisterDebug(debugCommands ?? server?.Settings?.NcDebugCommands ?? true);
        RegisterTransfer();
        RegisterDifficulty();
        RegisterDefaultGameMode();
        RegisterSave();
        RegisterDamage();
        RegisterParticle();
        RegisterPlaySound();
        RegisterStopSound();
        RegisterTick();
        RegisterPerf();
        RegisterSetWorldSpawn();
        RegisterRotate();
        RegisterWhiteList();
        RegisterClone();
        RegisterSpawnPoint();
        RegisterSetIdleTimeout();
        RegisterFillBiome();
        RegisterExperience();
        RegisterAttribute();
        RegisterTag();
        RegisterRecipe();
        RegisterSpectate();
        RegisterEffect();
    }

    public CommandDispatcher<CommandSourceStack> Dispatcher => _dispatcher;

    //SendCommands 把命令树下发给玩家 进入世界时调用
    //只下放该玩家有权执行的节点 对应原版 Commands.fillUsableCommands
    public void SendCommands(ServerPlayer player)
        => player.Connection.Send(new ClientboundCommandsPacket(BuildUsableTree(player)));

    //BuildUsableTree 按玩家当前权限裁剪命令树
    private RootCommandNode<CommandSourceStack> BuildUsableTree(ServerPlayer player)
    {
        var source = new ServerCommandSource(player, _server);
        var root = new RootCommandNode<CommandSourceStack>();
        CopyUsable(_dispatcher.GetRoot(), root, source);
        return root;
    }

    //CopyUsable 深度优先复制可用节点 复制节点清空权限谓词 无权访问的 redirect 一并摘掉
    private static void CopyUsable(CommandNode<CommandSourceStack> node, CommandNode<CommandSourceStack> target, CommandSourceStack source)
    {
        foreach (var child in node.GetChildren())
        {
            if (!child.CanUse(source)) continue;
            var builder = child.CreateBuilder();
            builder.Requires(_ => true);
            if (builder.GetRedirect() is { } redirect && !redirect.CanUse(source))
                builder.Forward(null, null, false);
            var copy = builder.Build();
            target.AddChild(copy);
            CopyUsable(child, copy, source);
        }
    }

    //Execute 执行玩家命令 解析失败与语法错误按原版回执给执行者
    public int Execute(ServerPlayer player, string command)
    {
        var source = new ServerCommandSource(player, _server);
        try
        {
            return _dispatcher.Execute(command, source);
        }
        catch (CommandSyntaxException e)
        {
            source.SendFailure(e.Message);
            return 0;
        }
    }

    //Execute 执行任意来源的命令 供控制台一类没有执行玩家的场合
    //玩家来源走上面那个重载 这里对应原版 performPrefixedCommand 的通用形态
    public int Execute(CommandSourceStack source, string command)
    {
        try
        {
            return _dispatcher.Execute(command, source);
        }
        catch (CommandSyntaxException e)
        {
            source.SendFailure(e.Message);
            return 0;
        }
    }

    //_currentExecutionContext 线程内正在跑的执行上下文 对应原版 CURRENT_EXECUTION_CONTEXT
    private static readonly ThreadLocal<ExecutionContext<CommandSourceStack>?> _currentExecutionContext = new();

    //ExecuteInContext 在执行上下文里排命令并消费队列对应原版 Commands.executeCommandInContext
    //函数执行嵌套命令时复用线程上已有的上下文 队列只由最外层消费
    public void ExecuteInContext(CommandSourceStack source, Action<ExecutionContext<CommandSourceStack>> configure)
    {
        var current = _currentExecutionContext.Value;
        if (current is not null)
        {
            configure(current);
            return;
        }
        //链长与分叉上限取游戏规则 对应原版 MAX_COMMAND_SEQUENCE_LENGTH 与 MAX_COMMAND_FORKS
        var chainLimit = Math.Max(1, _server.GameRules.GetInt(NetCraft.Game.World.Level.GameRules.MaxCommandSequenceLength));
        var forkLimit = Math.Max(1, _server.GameRules.GetInt(NetCraft.Game.World.Level.GameRules.MaxCommandForks));
        //System.Threading 也有个非泛型 ExecutionContext 全限定免歧义
        using var context = new NetCraft.Commands.Execution.ExecutionContext<CommandSourceStack>(chainLimit, forkLimit, Profiler.Get());
        _currentExecutionContext.Value = context;
        try
        {
            configure(context);
            context.RunCommandQueue();
        }
        finally
        {
            _currentExecutionContext.Value = null;
        }
    }

    //GetCompletions 按玩家权限算补全候选 对应原版 CommandDispatcher.getCompletionSuggestions
    //客户端按 tab 时发补全请求 这里算完回建议包 实体/物品这类候选只能服务端给
    public Suggestions GetCompletions(ServerPlayer player, string command)
        => GetCompletions(new ServerCommandSource(player, _server), command, command.Length);

    //GetCompletions 按任意命令源算补全候选 服务端控制台终端走这条
    //只解析到光标处 光标后面的内容不参与 与原版客户端补全的做法一致
    public Suggestions GetCompletions(CommandSourceStack source, string command, int cursor)
    {
        var head = cursor >= command.Length ? command : command[..cursor];
        var parse = _dispatcher.Parse(head, source);
        return _dispatcher.GetCompletionSuggestions(parse, cursor).GetAwaiter().GetResult();
    }

    //RequirePlayer 取玩家命令源 本作暂无控制台/命令方块来源
    private static ServerCommandSource? RequirePlayer(CommandContext<CommandSourceStack> context)
        => context.GetSource() as ServerCommandSource;

    //RegisterGameMode 注册 /gamemode <gamemode> [targets] 对应原版 gamemode 命令树
    private void RegisterGameMode()
    {
        _dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("gamemode")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, GameType>.Argument("gamemode", GameModeArgument.GameMode())
                .Executes(context => SetGameMode(context, new[] { ((ServerCommandSource)context.GetSource()).Player }))
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Executes(context => SetGameMode(context, EntityArgument.GetPlayers(context, "targets"))))));
    }

    //SetGameMode 切换目标玩家游戏模式并回执 对应原版 setGameMode
    //执行者自己只回执给自己 其他目标额外收到一条私人提示
    private static int SetGameMode(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer> targets)
    {
        var source = RequirePlayer(context);
        if (source is null) return 0;
        var gameType = GameModeArgument.GetGameMode(context, "gamemode");
        var changed = 0;
        foreach (var target in targets)
        {
            //模式未变不发包不回执 与原版 setGameMode 返回 false 一致
            if (target.GameType == gameType) continue;
            source.Server.PlayerList.ChangeGameMode(target, gameType);
            changed++;
            if (ReferenceEquals(target, source.Player))
            {
                source.SendSuccess($"已将您的游戏模式设置为 {gameType.Name}");
            }
            else
            {
                source.SendSuccess($"已将 {target.Profile.Name} 的游戏模式设置为 {gameType.Name}");
                target.Connection.Send(new ClientboundSystemChatPacket(
                    Component.Literal($"您的游戏模式已被设置为 {gameType.Name}"), false));
            }
        }
        if (changed == 0)
        {
            source.SendFailure("目标已处于该游戏模式");
            return 0;
        }
        return changed;
    }

    //RegisterHelp 注册原版 help 命令树
    private void RegisterHelp()
        => HelpCommand.Register(_dispatcher);

    //RegisterTeleport 注册原版 teleport/tp 命令树
    private void RegisterTeleport()
        => TeleportCommand.Register(_dispatcher);

    //RegisterTime 注册原版 time 命令树
    private void RegisterTime()
        => TimeCommand.Register(_dispatcher);

    //RegisterDebug 注册 debug 命令树 排查世界方块数据用 默认禁用 需 server.properties 的 nc-debug-commands 打开
    private void RegisterDebug(bool enabled)
    {
        if (!enabled) return;
        DebugCommand.Register(_dispatcher);
    }

    //RegisterStop 注册 /stop 停止服务端 主循环退出前会全量刷盘
    private void RegisterStop()
        => _dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
            .Requires(s => s.HasPermission(4))
            .Executes(StopServer));

    //RegisterOp 注册 /op /deop 管理员名单读写
    private void RegisterOp()
        => OpCommand.Register(_dispatcher);

    //RegisterKick 注册 /kick 踢出命令
    private void RegisterKick()
        => KickCommand.Register(_dispatcher);

    //RegisterBan 注册 /ban /ban-ip /pardon /pardon-ip /banlist 封禁命令
    private void RegisterBan()
        => BanCommand.Register(_dispatcher);

    //RegisterListPlayers 注册 /list 在线玩家列表
    private void RegisterListPlayers()
        => ListPlayersCommand.Register(_dispatcher);

    //RegisterEmote 注册 /me 动作广播
    private void RegisterEmote()
        => EmoteCommand.Register(_dispatcher);

    //RegisterMsg 注册 /msg /tell /w 私聊
    private void RegisterMsg()
        => MsgCommand.Register(_dispatcher);

    //RegisterSeed 注册 /seed 世界种子
    private void RegisterSeed()
        => SeedCommand.Register(_dispatcher);

    //RegisterVersion 注册 /version 服务端版本
    private void RegisterVersion()
        => VersionCommand.Register(_dispatcher);

    //RegisterSwing 注册 /swing 挥手动画
    private void RegisterSwing()
        => SwingCommand.Register(_dispatcher);

    //RegisterStopwatch 注册 /stopwatch 调试计时器
    private void RegisterStopwatch()
        => StopwatchCommand.Register(_dispatcher);

    //RegisterTransfer 注册 /transfer 转交到其他服务器
    private void RegisterTransfer()
        => TransferCommand.Register(_dispatcher);

    //RegisterDifficulty 注册 /difficulty 难度
    private void RegisterDifficulty()
        => DifficultyCommand.Register(_dispatcher);

    //RegisterDefaultGameMode 注册 /defaultgamemode 默认游戏模式
    private void RegisterDefaultGameMode()
        => DefaultGameModeCommand.Register(_dispatcher);

    //RegisterSave 注册 /save-all /save-off /save-on 刷盘控制
    private void RegisterSave()
        => SaveCommand.Register(_dispatcher);

    //RegisterDamage 注册 /damage 造成伤害
    private void RegisterDamage()
        => DamageCommand.Register(_dispatcher);

    //RegisterParticle 注册 /particle 发送粒子
    private void RegisterParticle()
        => ParticleCommand.Register(_dispatcher);

    //RegisterPlaySound 注册 /playsound 播放音效
    private void RegisterPlaySound()
        => PlaySoundCommand.Register(_dispatcher);

    //RegisterStopSound 注册 /stopsound 停止音效
    private void RegisterStopSound()
        => StopSoundCommand.Register(_dispatcher);

    //RegisterTick 注册 /tick 刻速率与冻结控制
    private void RegisterTick()
        => TickCommand.Register(_dispatcher);

    //RegisterPerf 注册 /perf 性能记录
    private void RegisterPerf()
        => PerfCommand.Register(_dispatcher);

    //RegisterSetWorldSpawn 注册 /setworldspawn 世界出生点
    private void RegisterSetWorldSpawn()
        => SetWorldSpawnCommand.Register(_dispatcher);

    //RegisterRotate 注册 /rotate 朝向控制
    private void RegisterRotate()
        => RotateCommand.Register(_dispatcher);

    //RegisterWhiteList 注册 /whitelist 白名单
    private void RegisterWhiteList()
        => WhiteListCommand.Register(_dispatcher);

    //RegisterClone 注册 /clone 区域复制
    private void RegisterClone()
        => CloneCommand.Register(_dispatcher);

    //RegisterSpawnPoint 注册 /spawnpoint 个人重生点
    private void RegisterSpawnPoint()
        => SpawnPointCommand.Register(_dispatcher);

    //RegisterSetIdleTimeout 注册 /setidletimeout 挂机踢出时长
    private void RegisterSetIdleTimeout()
        => SetIdleTimeoutCommand.Register(_dispatcher);

    //RegisterFillBiome 注册 /fillbiome 区域群系替换
    private void RegisterFillBiome()
        => FillBiomeCommand.Register(_dispatcher);

    //RegisterExperience 注册 /experience 玩家经验值与等级
    private void RegisterExperience()
        => ExperienceCommand.Register(_dispatcher);

    //RegisterAttribute 注册 /attribute 实体属性读写
    private void RegisterAttribute()
        => AttributeCommand.Register(_dispatcher);

    //RegisterTag 注册 /tag 实体标签
    private void RegisterTag()
        => TagCommand.Register(_dispatcher);

    //RegisterRecipe 注册 /recipe 玩家配方
    private void RegisterRecipe()
        => RecipeCommand.Register(_dispatcher);

    //RegisterSpectate 注册原版 spectate 命令树
    private void RegisterSpectate()
        => SpectateCommand.Register(_dispatcher);

    //RegisterEffect 注册原版 effect 命令树
    private void RegisterEffect()
        => EffectCommand.Register(_dispatcher);

    //StopServer 触发服务端关闭 回执先于 Stop 发出因 Stop 会断开玩家连接
    private static int StopServer(CommandContext<CommandSourceStack> context)
    {
        var source = RequirePlayer(context);
        if (source is null) return 0;
        source.SendSuccess("正在停止服务器");
        source.Server.Stop();
        return 1;
    }

    //RegisterClear 注册原版 clear 命令树
    private void RegisterClear()
        => ClearCommand.Register(_dispatcher);

    //RegisterKill 注册原版 kill 命令树
    private void RegisterKill()
        => KillCommand.Register(_dispatcher);

    //RegisterSetBlock 注册原版 setblock 命令树
    private void RegisterSetBlock()
        => SetBlockCommand.Register(_dispatcher);

    //RegisterFill 注册原版 fill 命令树
    private void RegisterFill()
        => FillCommand.Register(_dispatcher);

    //RegisterGameRule 注册原版 gamerule 命令树
    private void RegisterGameRule()
        => GameRuleCommand.Register(_dispatcher);

    //RegisterWorldBorder 注册原版 worldborder 命令树
    private void RegisterWorldBorder()
        => WorldBorderCommand.Register(_dispatcher);

    //RegisterWeather 注册原版 weather 命令树
    private void RegisterWeather()
        => WeatherCommand.Register(_dispatcher);

    //RegisterForceLoad 注册原版 forceload 命令树
    private void RegisterForceLoad()
        => ForceLoadCommand.Register(_dispatcher);

    //RegisterSpreadPlayers 注册原版 spreadplayers 命令树
    private void RegisterSpreadPlayers()
        => SpreadPlayersCommand.Register(_dispatcher);

    //RegisterSay 注册原版 say 命令树
    private void RegisterSay()
        => SayCommand.Register(_dispatcher);

    //RegisterTellraw 注册原版 tellraw 命令树
    private void RegisterTellraw()
        => TellrawCommand.Register(_dispatcher);

    //RegisterTitle 注册原版 title 命令树
    private void RegisterTitle()
        => TitleCommand.Register(_dispatcher);

    //RegisterGive 注册 /give <targets> <item> [count] 对应原版 give 命令树
    private void RegisterGive()
    {
        _dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("give")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Then(RequiredArgumentBuilder<CommandSourceStack, ItemInput>.Argument("item", ItemArgument.Item())
                    .Executes(context => Give(context, 1))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("count", IntegerArgumentType.Integer(1))
                        .Executes(context => Give(context, IntegerArgumentType.GetInteger(context, "count")))))));
    }

    //RegisterItem 注册原版 item 命令树 按槽位名读写物品
    private void RegisterItem()
        => ItemCommand.Register(_dispatcher);

    //RegisterData 注册原版 data 命令树 读写方块实体/实体/命令存储的 NBT
    private void RegisterData()
        => DataCommand.Register(_dispatcher);

    //Give 走内核给予入口给予物品 对应原版 give 命令的 inventory.add 加 drop 组合
    //放得下的进背包 放不下的由内核弹在玩家脚下 实际放入数量由内核返回
    private static int Give(CommandContext<CommandSourceStack> context, int count)
    {
        var source = RequirePlayer(context);
        if (source is null) return 0;
        var input = ItemArgument.GetItemInput(context, "item");
        var players = EntityArgument.GetPlayers(context, "targets");
        var name = input.Id.ToShortString();
        var given = 0;
        foreach (var player in players)
        {
            var stack = new ItemStack(input.Item.BuiltInRegistryHolder, count, input.Components);
            var placed = player.GiveItem(stack);
            if (placed == 0)
            {
                source.SendFailure($"玩家 {player.Profile.Name} 的背包已满");
                continue;
            }
            if (ReferenceEquals(player, source.Player))
            {
                source.SendSuccess($"已给予 {placed} 个 {name}");
            }
            else
            {
                source.SendSuccess($"已给予 {player.Profile.Name} {placed} 个 {name}");
                player.Connection.Send(new ClientboundSystemChatPacket(
                    Component.Literal($"已获得 {placed} 个 {name}"), false));
            }
            given++;
        }
        return given;
    }
}
