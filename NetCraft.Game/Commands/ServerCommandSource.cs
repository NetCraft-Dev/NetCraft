using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Logging;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//ServerCommandSource 服务端命令源对应原版 CommandSourceStack 的服务端部分
//分两种: 玩家发起持执行玩家回执走系统聊天包 控制台发起无玩家权限拉满回执走日志
public sealed class ServerCommandSource : CommandSourceStack
{
    //玩家发起 权限等级取玩家当前等级 0-4 由 ops.json 名单决定
    public ServerCommandSource(ServerPlayer player, MinecraftServer server)
        : base(player.Profile.Name, player.PermissionLevel, TextWriter.Null)
    {
        Player = player;
        Server = server;
    }

    //控制台发起 名字与权限由调用方给
    private ServerCommandSource(string name, int permissionLevel, MinecraftServer server, TextWriter? output = null,
        bool acceptsSuccess = true, bool acceptsFailure = true)
        : base(name, permissionLevel, output ?? TextWriter.Null, acceptsSuccess, acceptsFailure)
    {
        Player = null;
        Server = server;
    }

    //Console 控制台命令源 供服务端 GUI 输入框一类没有执行实体的场合
    //对应原版 DedicatedServer.createCommandSourceStack 发送者名 Server 权限取满
    public static ServerCommandSource Console(MinecraftServer server) => new("Server", 4, server);

    //GameLoop 游戏循环命令源对应原版 ServerFunctionManager.getGameLoopSender
    //名字 Server 权限 gamemasters 输出抑制
    public static ServerCommandSource GameLoop(MinecraftServer server)
        => new("Server", (int)NetCraft.Registry.PermissionLevel.Gamemasters, server, null, false, false);

    //Rcon RCON 命令源 回执写进调用方给的缓冲对应原版 RconConsoleSource.createCommandSourceStack
    //名字 Rcon 权限 LevelBasedPermissionSet.OWNER 即等级 4
    //PermissionLevel 在此类型里被基类的 PermissionLevel 属性遮住 只能全限定
    public static ServerCommandSource Rcon(MinecraftServer server, TextWriter output)
        => new("Rcon", (int)NetCraft.Registry.PermissionLevel.Owners, server, output);

    //Player 命令执行者 控制台发起时为 null
    public ServerPlayer? Player { get; }

    //命令需要玩家执行但来源没有玩家 对应原版 CommandSourceStack.ERROR_NOT_PLAYER
    public static readonly SimpleCommandExceptionType ErrorRequiresPlayer =
        new(new TranslatableMessage("permissions.requires.player"));

    //PlayerOrThrow 取执行玩家 控制台来源没有玩家时抛语法错误
    //对应原版 CommandSourceStack.getPlayerOrException 命令里要用执行者自身的都走它
    public ServerPlayer PlayerOrThrow => Player ?? throw ErrorRequiresPlayer.Create();

    //Server 服务端引用 供选择器取在线玩家与广播
    //类型取基类 MinecraftServer 对齐原版 CommandSourceStack.getServer
    public MinecraftServer Server { get; }

    //Position 命令源坐标 对应原版 CommandSourceStack.getPosition
    //控制台源没有实体 取原点
    public Vec3 Position => Player?.Position ?? new Vec3(0, 0, 0);

    public override void SendSuccess(string message)
        => Send(message, AcceptsSuccess);

    public override void SendFailure(string message)
        => Send(message, AcceptsFailure);

    //Send 单条回执 玩家发起的发系统聊天包 overlay 为 false 走聊天栏
    //带输出缓冲的(如 RCON)写回缓冲对应原版 RconConsoleSource 的缓冲行为
    //都没有的落到日志 对应原版打到服务端控制台
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
