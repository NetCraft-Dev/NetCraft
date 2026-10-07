using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//SpectateCommand spectate command, maps to vanilla net.minecraft.server.commands.SpectateCommand
//<target> [<player>] switches a spectator's view to the target entity; without arguments the view returns to itself
public static class SpectateCommand
{
    //ErrorSelf cannot spectate yourself, maps to vanilla ERROR_SELF
    private static readonly SimpleCommandExceptionType ErrorSelf =
        new(new LiteralMessage("you cannot spectate yourself"));

    //ErrorNotSpectator the given player is not a spectator, maps to vanilla ERROR_NOT_SPECTATOR
    private static readonly DynamicCommandExceptionType ErrorNotSpectator =
        new(name => new LiteralMessage($"{name} is not a spectator"));

    //ErrorCannotSpectate the target type cannot be spectated, maps to vanilla ERROR_CANNOT_SPECTATE
    private static readonly DynamicCommandExceptionType ErrorCannotSpectate =
        new(name => new LiteralMessage($"cannot spectate {name}"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("spectate")
            .Requires(s => s.HasPermission(2))
            .Executes(context =>
            {
                var source = (ServerCommandSource)context.GetSource();
                return Spectate(source, null, source.PlayerOrThrow);
            })
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("target", EntityArgument.Entity())
                .Executes(context =>
                {
                    var source = (ServerCommandSource)context.GetSource();
                    return Spectate(source, EntityArgument.GetSingleTarget(context, "target"), source.PlayerOrThrow);
                })
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("player", EntityArgument.Player())
                    .Executes(context => Spectate((ServerCommandSource)context.GetSource(),
                        EntityArgument.GetSingleTarget(context, "target"), EntityArgument.GetPlayer(context, "player"))))));
    }

    //Spectate switches the target player's spectator camera, maps to vanilla SpectateCommand.spectate
    //target null means restoring the player's own view; validation order and errors match vanilla
    private static int Spectate(ServerCommandSource source, CommandTarget? target, ServerPlayer player)
    {
        if (target is not null && target.EntityId == player.EntityId)
            throw ErrorSelf.Create();
        if (!player.IsSpectator)
            throw ErrorNotSpectator.Create(player.Profile.Name);
        //Entities with tracking range 0 get no AddEntity and the client cannot see them, so they cannot be spectated, maps to vanilla clientTrackingRange
        if (target?.Type is { TrackingRangeChunks: 0 })
            throw ErrorCannotSpectate.Create(target.Name);
        player.SetCamera(target?.Player ?? (ITrackedEntity?)target?.WorldEntity);
        if (target is not null)
            source.SendSuccess($"spectating {target.Name}");
        else
            source.SendSuccess("stopped spectating");
        return 1;
    }
}
