using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Logging;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//ServerCommandSource server command source, maps to the server part of vanilla CommandSourceStack
//Two kinds: a player-initiated one holding the executing player and replying via the system chat packet; a console-initiated one with no player and full permission, replying via the log
public sealed class ServerCommandSource : CommandSourceStack
{
    //Player-initiated; the permission level is the player's current level 0-4 from the ops.json list
    public ServerCommandSource(ServerPlayer player, MinecraftServer server)
        : base(player.Profile.Name, player.PermissionLevel, TextWriter.Null)
    {
        Player = player;
        Server = server;
    }

    //Console-initiated; name and permission are given by the caller
    private ServerCommandSource(string name, int permissionLevel, MinecraftServer server, TextWriter? output = null,
        bool acceptsSuccess = true, bool acceptsFailure = true)
        : base(name, permissionLevel, output ?? TextWriter.Null, acceptsSuccess, acceptsFailure)
    {
        Player = null;
        Server = server;
    }

    //Console the console command source, for cases with no executing entity such as the server GUI input box
    //maps to vanilla DedicatedServer.createCommandSourceStack with sender name Server and full permission
    public static ServerCommandSource Console(MinecraftServer server) => new("Server", 4, server);

    //GameLoop the game loop command source, maps to vanilla ServerFunctionManager.getGameLoopSender
    //Name Server, permission gamemasters, output suppressed
    public static ServerCommandSource GameLoop(MinecraftServer server)
        => new("Server", (int)NetCraft.Registry.PermissionLevel.Gamemasters, server, null, false, false);

    //Rcon the RCON command source, replying into the caller-provided buffer, maps to vanilla RconConsoleSource.createCommandSourceStack
    //Name Rcon, permission LevelBasedPermissionSet.OWNER i.e. level 4
    //PermissionLevel is shadowed by the base class PermissionLevel property here, so it must be fully qualified
    public static ServerCommandSource Rcon(MinecraftServer server, TextWriter output)
        => new("Rcon", (int)NetCraft.Registry.PermissionLevel.Owners, server, output);

    //Player the command executor, null when console-initiated
    public ServerPlayer? Player { get; }

    //The command needs a player but the source has none, maps to vanilla CommandSourceStack.ERROR_NOT_PLAYER
    public static readonly SimpleCommandExceptionType ErrorRequiresPlayer =
        new(new TranslatableMessage("permissions.requires.player"));

    //PlayerOrThrow gets the executing player; a console source with no player throws a syntax error
    //maps to vanilla CommandSourceStack.getPlayerOrException; anything in a command needing the executor itself goes through this
    public ServerPlayer PlayerOrThrow => Player ?? throw ErrorRequiresPlayer.Create();

    //Server server reference, for selectors to get online players and broadcast
    //The type is the base class MinecraftServer, aligned with vanilla CommandSourceStack.getServer
    public MinecraftServer Server { get; }

    //Position the command source coordinate, maps to vanilla CommandSourceStack.getPosition
    //A console source has no entity, so the origin is used
    public Vec3 Position => Player?.Position ?? new Vec3(0, 0, 0);

    public override void SendSuccess(string message)
        => Send(message, AcceptsSuccess);

    public override void SendFailure(string message)
        => Send(message, AcceptsFailure);

    //Send a single reply; a player-initiated one sends the system chat packet with overlay false through the chat bar
    //One with an output buffer (such as RCON) writes back to the buffer, maps to vanilla RconConsoleSource's buffer behavior
    //None of those falls back to the log, maps to vanilla printing to the server console
    private void Send(string message, bool accepted)
    {
        if (!accepted) return;
        if (Player is { } player)
            player.Connection.Send(new ClientboundSystemChatPacket(Component.Literal(message), false));
        else if (!ReferenceEquals(Output, TextWriter.Null))
            Output.WriteLine(message);
        else
            Log.Info($"[Server] {message}");
    }
}
