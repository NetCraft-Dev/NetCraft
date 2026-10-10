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
using NetCraft.Game.World.Items.Component;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Profiling;

namespace NetCraft.Game.Commands;

//CommandManager command manager, maps to vanilla net.minecraft.commands.Commands
//Holds the dispatcher, registers built-in commands, provides command tree dispatch and the player command execution entry
public sealed class CommandManager
{
    private readonly CommandDispatcher<CommandSourceStack> _dispatcher = new();
    private readonly MinecraftServer _server;

    //debugCommands explicitly specifies whether to register /debug; null takes nc-debug-commands from server.properties
    //This parameter exists so tests can enable the debug tree without server.properties
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
        //server null only happens in tests using the command tree construction directly; treated as on, production always reads server.properties
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

    //SendCommands sends the command tree to a player, called when entering the world
    //Only dispatches nodes the player has permission to run, maps to vanilla Commands.fillUsableCommands
    public void SendCommands(ServerPlayer player)
        => player.Connection.Send(new ClientboundCommandsPacket(BuildUsableTree(player)));

    //BuildUsableTree trims the command tree by the player's current permissions
    private RootCommandNode<CommandSourceStack> BuildUsableTree(ServerPlayer player)
    {
        var source = new ServerCommandSource(player, _server);
        var root = new RootCommandNode<CommandSourceStack>();
        CopyUsable(_dispatcher.GetRoot(), root, source);
        return root;
    }

    //CopyUsable depth-first copies usable nodes; copied nodes have their permission predicate cleared and inaccessible redirects removed
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

    //Execute runs a player command; parse failures and syntax errors are reported to the executor like vanilla
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

    //Execute runs a command from any source, for cases with no executing player such as the console
    //Player sources go through the overload above; this maps to the general form of vanilla performPrefixedCommand
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

    //_currentExecutionContext the execution context running on this thread, maps to vanilla CURRENT_EXECUTION_CONTEXT
    private static readonly ThreadLocal<ExecutionContext<CommandSourceStack>?> _currentExecutionContext = new();

    //ExecuteInContext queues commands in the execution context and drains the queue, maps to vanilla Commands.executeCommandInContext
    //When a function runs a nested command it reuses the thread's existing context; the queue is drained only by the outermost layer
    public void ExecuteInContext(CommandSourceStack source, Action<ExecutionContext<CommandSourceStack>> configure)
    {
        var current = _currentExecutionContext.Value;
        if (current is not null)
        {
            configure(current);
            return;
        }
        //The chain length and fork limits come from gamerules, maps to vanilla MAX_COMMAND_SEQUENCE_LENGTH and MAX_COMMAND_FORKS
        var chainLimit = Math.Max(1, _server.GameRules.GetInt(NetCraft.Game.World.Level.GameRules.MaxCommandSequenceLength));
        var forkLimit = Math.Max(1, _server.GameRules.GetInt(NetCraft.Game.World.Level.GameRules.MaxCommandForks));
        //System.Threading also has a non-generic ExecutionContext; fully qualified to avoid ambiguity
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

    //GetCompletions computes completion candidates by player permission, maps to vanilla CommandDispatcher.getCompletionSuggestions
    //The client sends a completion request on tab; this computes and replies with a suggestion packet; candidates like entities/items can only come from the server
    public Suggestions GetCompletions(ServerPlayer player, string command)
        => GetCompletions(new ServerCommandSource(player, _server), command, command.Length);

    //GetCompletions computes completion candidates for any command source; the server console terminal goes through this
    //Only parses up to the cursor; content after the cursor does not participate, consistent with vanilla client completion
    public Suggestions GetCompletions(CommandSourceStack source, string command, int cursor)
    {
        var head = cursor >= command.Length ? command : command[..cursor];
        var parse = _dispatcher.Parse(head, source);
        return _dispatcher.GetCompletionSuggestions(parse, cursor).GetAwaiter().GetResult();
    }

    //RequirePlayer gets the player command source; this project has no console/command block source yet
    private static ServerCommandSource? RequirePlayer(CommandContext<CommandSourceStack> context)
        => context.GetSource() as ServerCommandSource;

    //RegisterGameMode registers /gamemode <gamemode> [targets], maps to the vanilla gamemode command tree
    private void RegisterGameMode()
    {
        _dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("gamemode")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, GameType>.Argument("gamemode", GameModeArgument.GameMode())
                .Executes(context => SetGameMode(context, new[] { ((ServerCommandSource)context.GetSource()).Player }))
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Executes(context => SetGameMode(context, EntityArgument.GetPlayers(context, "targets"))))));
    }

    //SetGameMode switches the target player's game mode and reports, maps to vanilla setGameMode
    //The executor gets only its own report; other targets also receive a private notice
    private static int SetGameMode(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer> targets)
    {
        var source = RequirePlayer(context);
        if (source is null) return 0;
        var gameType = GameModeArgument.GetGameMode(context, "gamemode");
        var changed = 0;
        foreach (var target in targets)
        {
            //No packet or report when the mode is unchanged, consistent with vanilla setGameMode returning false
            if (target.GameType == gameType) continue;
            source.Server.PlayerList.ChangeGameMode(target, gameType);
            changed++;
            if (ReferenceEquals(target, source.Player))
            {
                source.SendSuccess($"set your game mode to {gameType.Name}");
            }
            else
            {
                source.SendSuccess($"set {target.Profile.Name}'s game mode to {gameType.Name}");
                target.Connection.Send(new ClientboundSystemChatPacket(
                    Component.Literal($"your game mode has been set to {gameType.Name}"), false));
            }
        }
        if (changed == 0)
        {
            source.SendFailure("the target is already in that game mode");
            return 0;
        }
        return changed;
    }

    //RegisterHelp registers the vanilla help command tree
    private void RegisterHelp()
        => HelpCommand.Register(_dispatcher);

    //RegisterTeleport registers the vanilla teleport/tp command tree
    private void RegisterTeleport()
        => TeleportCommand.Register(_dispatcher);

    //RegisterTime registers the vanilla time command tree
    private void RegisterTime()
        => TimeCommand.Register(_dispatcher);

    //RegisterDebug registers the debug command tree for inspecting world block data; disabled by default, enable with nc-debug-commands in server.properties
    private void RegisterDebug(bool enabled)
    {
        if (!enabled) return;
        DebugCommand.Register(_dispatcher);
    }

    //RegisterStop registers /stop to stop the server; it flushes everything before the main loop exits
    private void RegisterStop()
        => _dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
            .Requires(s => s.HasPermission(4))
            .Executes(StopServer));

    //RegisterOp registers /op /deop for reading and writing the operator list
    private void RegisterOp()
        => OpCommand.Register(_dispatcher);

    //RegisterKick registers the /kick command
    private void RegisterKick()
        => KickCommand.Register(_dispatcher);

    //RegisterBan registers /ban /ban-ip /pardon /pardon-ip /banlist
    private void RegisterBan()
        => BanCommand.Register(_dispatcher);

    //RegisterListPlayers registers /list for the online player list
    private void RegisterListPlayers()
        => ListPlayersCommand.Register(_dispatcher);

    //RegisterEmote registers /me action broadcast
    private void RegisterEmote()
        => EmoteCommand.Register(_dispatcher);

    //RegisterMsg registers /msg /tell /w private message
    private void RegisterMsg()
        => MsgCommand.Register(_dispatcher);

    //RegisterSeed registers /seed world seed
    private void RegisterSeed()
        => SeedCommand.Register(_dispatcher);

    //RegisterVersion registers /version server version
    private void RegisterVersion()
        => VersionCommand.Register(_dispatcher);

    //RegisterSwing registers /swing swing animation
    private void RegisterSwing()
        => SwingCommand.Register(_dispatcher);

    //RegisterStopwatch registers /stopwatch debug timer
    private void RegisterStopwatch()
        => StopwatchCommand.Register(_dispatcher);

    //RegisterTransfer registers /transfer hand-off to another server
    private void RegisterTransfer()
        => TransferCommand.Register(_dispatcher);

    //RegisterDifficulty registers /difficulty
    private void RegisterDifficulty()
        => DifficultyCommand.Register(_dispatcher);

    //RegisterDefaultGameMode registers /defaultgamemode
    private void RegisterDefaultGameMode()
        => DefaultGameModeCommand.Register(_dispatcher);

    //RegisterSave registers /save-all /save-off /save-on flush control
    private void RegisterSave()
        => SaveCommand.Register(_dispatcher);

    //RegisterDamage registers /damage to deal damage
    private void RegisterDamage()
        => DamageCommand.Register(_dispatcher);

    //RegisterParticle registers /particle to emit particles
    private void RegisterParticle()
        => ParticleCommand.Register(_dispatcher);

    //RegisterPlaySound registers /playsound to play a sound
    private void RegisterPlaySound()
        => PlaySoundCommand.Register(_dispatcher);

    //RegisterStopSound registers /stopsound to stop sounds
    private void RegisterStopSound()
        => StopSoundCommand.Register(_dispatcher);

    //RegisterTick registers /tick tick rate and freeze control
    private void RegisterTick()
        => TickCommand.Register(_dispatcher);

    //RegisterPerf registers /perf performance recording
    private void RegisterPerf()
        => PerfCommand.Register(_dispatcher);

    //RegisterSetWorldSpawn registers /setworldspawn
    private void RegisterSetWorldSpawn()
        => SetWorldSpawnCommand.Register(_dispatcher);

    //RegisterRotate registers /rotate facing control
    private void RegisterRotate()
        => RotateCommand.Register(_dispatcher);

    //RegisterWhiteList registers /whitelist
    private void RegisterWhiteList()
        => WhiteListCommand.Register(_dispatcher);

    //RegisterClone registers /clone region copy
    private void RegisterClone()
        => CloneCommand.Register(_dispatcher);

    //RegisterSpawnPoint registers /spawnpoint personal respawn point
    private void RegisterSpawnPoint()
        => SpawnPointCommand.Register(_dispatcher);

    //RegisterSetIdleTimeout registers /setidletimeout idle kick timeout
    private void RegisterSetIdleTimeout()
        => SetIdleTimeoutCommand.Register(_dispatcher);

    //RegisterFillBiome registers /fillbiome region biome replacement
    private void RegisterFillBiome()
        => FillBiomeCommand.Register(_dispatcher);

    //RegisterExperience registers /experience player xp and level
    private void RegisterExperience()
        => ExperienceCommand.Register(_dispatcher);

    //RegisterAttribute registers /attribute entity attribute read/write
    private void RegisterAttribute()
        => AttributeCommand.Register(_dispatcher);

    //RegisterTag registers /tag entity tags
    private void RegisterTag()
        => TagCommand.Register(_dispatcher);

    //RegisterRecipe registers /recipe player recipes
    private void RegisterRecipe()
        => RecipeCommand.Register(_dispatcher);

    //RegisterSpectate registers the vanilla spectate command tree
    private void RegisterSpectate()
        => SpectateCommand.Register(_dispatcher);

    //RegisterEffect registers the vanilla effect command tree
    private void RegisterEffect()
        => EffectCommand.Register(_dispatcher);

    //StopServer triggers server shutdown; the report is sent before Stop because Stop disconnects players
    private static int StopServer(CommandContext<CommandSourceStack> context)
    {
        var source = RequirePlayer(context);
        if (source is null) return 0;
        source.SendSuccess("stopping the server");
        source.Server.Stop();
        return 1;
    }

    //RegisterClear registers the vanilla clear command tree
    private void RegisterClear()
        => ClearCommand.Register(_dispatcher);

    //RegisterKill registers the vanilla kill command tree
    private void RegisterKill()
        => KillCommand.Register(_dispatcher);

    //RegisterSetBlock registers the vanilla setblock command tree
    private void RegisterSetBlock()
        => SetBlockCommand.Register(_dispatcher);

    //RegisterFill registers the vanilla fill command tree
    private void RegisterFill()
        => FillCommand.Register(_dispatcher);

    //RegisterGameRule registers the vanilla gamerule command tree
    private void RegisterGameRule()
        => GameRuleCommand.Register(_dispatcher);

    //RegisterWorldBorder registers the vanilla worldborder command tree
    private void RegisterWorldBorder()
        => WorldBorderCommand.Register(_dispatcher);

    //RegisterWeather registers the vanilla weather command tree
    private void RegisterWeather()
        => WeatherCommand.Register(_dispatcher);

    //RegisterForceLoad registers the vanilla forceload command tree
    private void RegisterForceLoad()
        => ForceLoadCommand.Register(_dispatcher);

    //RegisterSpreadPlayers registers the vanilla spreadplayers command tree
    private void RegisterSpreadPlayers()
        => SpreadPlayersCommand.Register(_dispatcher);

    //RegisterSay registers the vanilla say command tree
    private void RegisterSay()
        => SayCommand.Register(_dispatcher);

    //RegisterTellraw registers the vanilla tellraw command tree
    private void RegisterTellraw()
        => TellrawCommand.Register(_dispatcher);

    //RegisterTitle registers the vanilla title command tree
    private void RegisterTitle()
        => TitleCommand.Register(_dispatcher);

    //RegisterGive registers /give <targets> <item> [count], maps to the vanilla give command tree
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

    //RegisterItem registers the vanilla item command tree, reading/writing items by slot name
    private void RegisterItem()
        => ItemCommand.Register(_dispatcher);

    //RegisterData registers the vanilla data command tree, reading/writing NBT of block entities/entities/command storage
    private void RegisterData()
        => DataCommand.Register(_dispatcher);

    //Give uses the core give entry, maps to the vanilla give command's inventory.add plus drop combination
    //What fits goes into the inventory; what does not is dropped at the player's feet by the core; the actual placed count is returned by the core
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
                source.SendFailure($"player {player.Profile.Name}'s inventory is full");
                continue;
            }
            if (ReferenceEquals(player, source.Player))
            {
                source.SendSuccess($"gave {placed} x {name}");
            }
            else
            {
                source.SendSuccess($"gave {player.Profile.Name} {placed} x {name}");
                player.Connection.Send(new ClientboundSystemChatPacket(
                    Component.Literal($"received {placed} x {name}"), false));
            }
            given++;
        }
        return given;
    }
}
