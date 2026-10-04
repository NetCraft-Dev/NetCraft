using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Network.Protocol.Login;
using NetCraft.Game.Server;
using NetCraft.Logging;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//BanCommand 封禁相关命令 对应原版 net.minecraft.server.commands.BanPlayerCommands 与 BanIpCommands
//ban/ban-ip 写入名单并踢掉在线目标 pardon/pardon-ip 解除 banlist 列出
//目标只解析在线玩家名 本作没有离线玩家档案解析(与 /op 同一限制) 离线玩家只能手写 json 或按名字解封
public static class BanCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("ban")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("targets", StringArgumentType.Word())
                .Executes(context => Ban(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>
                    .Argument("reason", StringArgumentType.GreedyString())
                    .Executes(context => Ban(context, StringArgumentType.GetString(context, "reason"))))));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("ban-ip")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                .Executes(context => BanIp(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>
                    .Argument("reason", StringArgumentType.GreedyString())
                    .Executes(context => BanIp(context, StringArgumentType.GetString(context, "reason"))))));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("pardon")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("targets", StringArgumentType.Word())
                .Executes(Pardon)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("pardon-ip")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                .Executes(PardonIp)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("banlist")
            .Requires(s => s.HasPermission(3))
            .Executes(context => ListBans(context, false))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("ips")
                .Executes(context => ListBans(context, true))));
    }

    //Ban 封禁玩家并踢掉在线目标 对应原版 banPlayers
    private static int Ban(CommandContext<CommandSourceStack> context, string? reason)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "targets");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"玩家 {name} 不在线 本作只能封禁在线玩家");
            return 0;
        }
        var text = reason ?? BanList.DefaultReason;
        if (!source.Server.BanList.Add(target.Profile, source.PlayerOrThrow.Profile.Name, text))
        {
            source.SendFailure($"玩家 {name} 已在封禁名单中");
            return 0;
        }
        //封禁同时把在线目标踢掉 对应原版 banPlayers 里的 disconnect
        target.Disconnect(reason is null
            ? Component.Translatable("disconnect.banned")
            : Component.Translatable("disconnect.banned.reason", text));
        Log.Info($"Banned player {target.Profile.Name} operator={source.PlayerOrThrow.Profile.Name} reason={text}");
        source.SendSuccess($"已封禁玩家 {name}");
        return 1;
    }

    //BanIp 封禁 IP 目标先按在线玩家名解析出 IP 再当作 IP 字面量 对应原版 banIps
    private static int BanIp(CommandContext<CommandSourceStack> context, string? reason)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = StringArgumentType.GetString(context, "target");
        var player = source.Server.PlayerList.GetPlayerByName(target);
        var address = player?.Connection.RemoteAddress ?? target;
        if (string.IsNullOrWhiteSpace(address))
        {
            source.SendFailure($"无法确定 {target} 的 IP");
            return 0;
        }
        var text = reason ?? BanList.DefaultReason;
        if (!source.Server.IpBanList.Add(address, source.PlayerOrThrow.Profile.Name, text))
        {
            source.SendFailure($"IP {address} 已在封禁名单中");
            return 0;
        }
        //把该 IP 上的在线玩家一并踢掉 对应原版 banIps 里的 disconnect
        //IP 封禁只有 disconnect.banned.ip 一个翻译键 理由不进断连文案
        foreach (var online in source.Server.PlayerList.Players)
        {
            if (!string.Equals(online.Connection.RemoteAddress, address, StringComparison.OrdinalIgnoreCase))
                continue;
            online.Disconnect(Component.Translatable("disconnect.banned.ip"));
        }
        Log.Info($"Banned IP {address} operator={source.PlayerOrThrow.Profile.Name} reason={text}");
        source.SendSuccess($"已封禁 IP {address}");
        return 1;
    }

    //Pardon 解除玩家封禁 在线玩家按档案命中 离线只按名字构造档案
    private static int Pardon(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "targets");
        var online = source.Server.PlayerList.GetPlayerByName(name);
        var profile = online?.Profile ?? new GameProfile(Guid.Empty, name);
        if (!source.Server.BanList.Remove(profile))
        {
            source.SendFailure($"玩家 {name} 不在封禁名单中");
            return 0;
        }
        Log.Info($"Unbanned {name} operator={source.PlayerOrThrow.Profile.Name}");
        source.SendSuccess($"已解除 {name} 的封禁");
        return 1;
    }

    //PardonIp 解除 IP 封禁
    private static int PardonIp(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var address = StringArgumentType.GetString(context, "target");
        if (!source.Server.IpBanList.Remove(address))
        {
            source.SendFailure($"IP {address} 不在封禁名单中");
            return 0;
        }
        Log.Info($"Unbanned IP {address} operator={source.PlayerOrThrow.Profile.Name}");
        source.SendSuccess($"已解除 IP {address} 的封禁");
        return 1;
    }

    //ListBans 列出封禁名单 ips 为 true 时列 IP 名单
    private static int ListBans(CommandContext<CommandSourceStack> context, bool ips)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (ips)
        {
            var list = source.Server.IpBanList.Entries;
            if (list.Count == 0)
            {
                source.SendSuccess("IP 封禁名单为空");
                return 0;
            }
            source.SendSuccess($"共 {list.Count} 个被封禁的 IP");
            foreach (var entry in list) source.SendSuccess($"- {entry.Ip} 理由: {entry.Reason} 操作者: {entry.Source}");
            return list.Count;
        }
        var bans = source.Server.BanList.Entries;
        if (bans.Count == 0)
        {
            source.SendSuccess("封禁名单为空");
            return 0;
        }
        source.SendSuccess($"共 {bans.Count} 个被封禁的玩家");
        foreach (var entry in bans) source.SendSuccess($"- {entry.Name} 理由: {entry.Reason} 操作者: {entry.Source}");
        return bans.Count;
    }
}
