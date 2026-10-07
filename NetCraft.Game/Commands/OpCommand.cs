using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//OpCommand operator commands op/deop read/write the ops.json list and sync online targets' permission level immediately
//Vanilla's target argument is a game profile that may include offline players; this only resolves online player names
public static class OpCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("op")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                .Executes(context => Apply(context, true))));
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("deop")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                .Executes(context => Apply(context, false))));
    }

    //Apply adds/removes from the list by the flag; online targets refresh their permission level and sync to the client immediately
    private static int Apply(CommandContext<CommandSourceStack> context, bool op)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "target");
        var target = source.Server.PlayerList.GetPlayerByName(name);
        if (target is null)
        {
            source.SendFailure($"player {name} is not online");
            return 0;
        }

        if (op)
        {
            var level = source.Server.Settings.OpPermissionLevel;
            source.Server.OpList.Add(target.Profile, level);
            source.Server.PlayerList.ApplyPermissionLevel(target, level);
            source.SendSuccess($"made {target.Profile.Name} an operator, permission level {level}");
            return 1;
        }

        source.Server.OpList.Remove(target.Profile);
        source.Server.PlayerList.ApplyPermissionLevel(target, 0);
        source.SendSuccess($"revoked {target.Profile.Name}'s operator permission");
        return 1;
    }
}
