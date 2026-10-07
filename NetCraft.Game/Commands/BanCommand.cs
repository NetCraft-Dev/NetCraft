using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Network.Protocol.Login;
using NetCraft.Game.Server;
using NetCraft.Logging;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//BanCommand ban-related commands, maps to vanilla net.minecraft.server.commands.BanPlayerCommands and BanIpCommands
//ban/ban-ip write to the lists and kick online targets; pardon/pardon-ip unban; banlist lists
//Targets only resolve online player names; this project has no offline player profile lookup (same limitation as /op), so offline players can only be unbanned by editing json or by name
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

    //Ban bans a player and kicks online targets, maps to vanilla banPlayers
    private static int Ban(CommandContext<CommandSourceStack> context, string? reason)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "targets");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"player {name} is not online; this project can only ban online players");
            return 0;
        }
        var text = reason ?? BanList.DefaultReason;
        if (!source.Server.BanList.Add(target.Profile, source.PlayerOrThrow.Profile.Name, text))
        {
            source.SendFailure($"player {name} is already on the ban list");
            return 0;
        }
        //Banning also kicks online targets, maps to vanilla banPlayers' disconnect
        target.Disconnect(reason is null
            ? Component.Translatable("disconnect.banned")
            : Component.Translatable("disconnect.banned.reason", text));
        Log.Info($"Banned player {target.Profile.Name} operator={source.PlayerOrThrow.Profile.Name} reason={text}");
        source.SendSuccess($"banned player {name}");
        return 1;
    }

    //BanIp bans an IP; the target is first resolved from an online player name to an IP, then treated as an IP literal, maps to vanilla banIps
    private static int BanIp(CommandContext<CommandSourceStack> context, string? reason)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = StringArgumentType.GetString(context, "target");
        var player = source.Server.PlayerList.GetPlayerByName(target);
        var address = player?.Connection.RemoteAddress ?? target;
        if (string.IsNullOrWhiteSpace(address))
        {
            source.SendFailure($"cannot determine the IP of {target}");
            return 0;
        }
        var text = reason ?? BanList.DefaultReason;
        if (!source.Server.IpBanList.Add(address, source.PlayerOrThrow.Profile.Name, text))
        {
            source.SendFailure($"IP {address} is already on the ban list");
            return 0;
        }
        //Kick the online players on that IP as well, maps to vanilla banIps' disconnect
        //IP bans only have the disconnect.banned.ip translation key; the reason does not go into the disconnect text
        foreach (var online in source.Server.PlayerList.Players)
        {
            if (!string.Equals(online.Connection.RemoteAddress, address, StringComparison.OrdinalIgnoreCase))
                continue;
            online.Disconnect(Component.Translatable("disconnect.banned.ip"));
        }
        Log.Info($"Banned IP {address} operator={source.PlayerOrThrow.Profile.Name} reason={text}");
        source.SendSuccess($"banned IP {address}");
        return 1;
    }

    //Pardon unbans a player; online players are matched by profile, offline ones only by name
    private static int Pardon(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "targets");
        var online = source.Server.PlayerList.GetPlayerByName(name);
        var profile = online?.Profile ?? new GameProfile(Guid.Empty, name);
        if (!source.Server.BanList.Remove(profile))
        {
            source.SendFailure($"player {name} is not on the ban list");
            return 0;
        }
        Log.Info($"Unbanned {name} operator={source.PlayerOrThrow.Profile.Name}");
        source.SendSuccess($"unbanned {name}");
        return 1;
    }

    //PardonIp removes an IP ban
    private static int PardonIp(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var address = StringArgumentType.GetString(context, "target");
        if (!source.Server.IpBanList.Remove(address))
        {
            source.SendFailure($"IP {address} is not on the ban list");
            return 0;
        }
        Log.Info($"Unbanned IP {address} operator={source.PlayerOrThrow.Profile.Name}");
        source.SendSuccess($"unbanned IP {address}");
        return 1;
    }

    //ListBans lists the ban list; when ips is true it lists the IP list
    private static int ListBans(CommandContext<CommandSourceStack> context, bool ips)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (ips)
        {
            var list = source.Server.IpBanList.Entries;
            if (list.Count == 0)
            {
                source.SendSuccess("the IP ban list is empty");
                return 0;
            }
            source.SendSuccess($"{list.Count} IPs are banned");
            foreach (var entry in list) source.SendSuccess($"- {entry.Ip} reason: {entry.Reason} source: {entry.Source}");
            return list.Count;
        }
        var bans = source.Server.BanList.Entries;
        if (bans.Count == 0)
        {
            source.SendSuccess("the ban list is empty");
            return 0;
        }
        source.SendSuccess($"{bans.Count} players are banned");
        foreach (var entry in bans) source.SendSuccess($"- {entry.Name} reason: {entry.Reason} source: {entry.Source}");
        return bans.Count;
    }
}
