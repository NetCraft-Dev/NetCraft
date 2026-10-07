using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//WhiteListCommand whitelist command, maps to vanilla net.minecraft.server.commands.WhitelistCommand
//Toggles the whitelist and adds/removes members; like vanilla the target only resolves online player names
public static class WhiteListCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("whitelist")
            .Requires(s => s.HasPermission(3))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("on")
                .Executes(context => SetEnabled(context, true)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("off")
                .Executes(context => SetEnabled(context, false)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
                .Executes(List))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                    .Executes(context => Apply(context, true))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                    .Executes(context => Apply(context, false))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("reload")
                .Executes(Reload)));
    }

    //SetEnabled toggles the whitelist and writes it back to server.properties
    private static int SetEnabled(CommandContext<CommandSourceStack> context, bool enabled)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (source.Server.IsWhiteListEnabled == enabled)
        {
            source.SendFailure(enabled ? "the whitelist is already on" : "the whitelist is already off");
            return 0;
        }

        source.Server.IsWhiteListEnabled = enabled;
        source.Server.Settings.SetWhiteList(enabled);
        source.Server.Settings.SaveCurrent();
        source.SendSuccess(enabled ? "whitelist enabled" : "whitelist disabled");
        return 1;
    }

    //List lists the members
    private static int List(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var names = source.Server.WhiteList.Names;
        source.SendSuccess($"the whitelist has {names.Count} members; it is currently {(source.Server.IsWhiteListEnabled ? "on" : "off")}");
        foreach (var name in names) source.SendSuccess(name);
        return names.Count;
    }

    //Apply adds/removes a member; only online players are recognized, unlike vanilla which supports offline profiles
    private static int Apply(CommandContext<CommandSourceStack> context, bool add)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "target");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"player {name} is not online");
            return 0;
        }

        if (add)
        {
            source.Server.WhiteList.Add(target.Profile);
            source.SendSuccess($"added {target.Profile.Name} to the whitelist");
            return 1;
        }

        if (!source.Server.WhiteList.Remove(target.Profile))
        {
            source.SendFailure($"{target.Profile.Name} is not on the whitelist");
            return 0;
        }

        source.SendSuccess($"removed {target.Profile.Name} from the whitelist");
        return 1;
    }

    //Reload re-reads the list from disk
    private static int Reload(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        source.Server.WhiteList.Reload();
        source.SendSuccess($"reloaded the whitelist, {source.Server.WhiteList.Count} entries");
        return 1;
    }
}
