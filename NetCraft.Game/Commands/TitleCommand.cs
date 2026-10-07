using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//TitleCommand title command, maps to vanilla net.minecraft.server.commands.TitleCommand
//Six branches clear/reset/title/subtitle/actionbar/times, targeting multiple players
//Components do no selector resolution, maps to vanilla getRawComponent rather than getResolvedComponent
public static class TitleCommand
{
    //SendTitleText the title body, shown in the screen center
    private static readonly Action<ServerPlayer, Component> SendTitleText =
        (player, text) => player.Connection.Send(new ClientboundSetTitleTextPacket(text));

    //SendSubtitleText the subtitle, shown below the title
    private static readonly Action<ServerPlayer, Component> SendSubtitleText =
        (player, text) => player.Connection.Send(new ClientboundSetSubtitleTextPacket(text));

    //SendActionBarText the action bar text, sent through the dedicated packet like vanilla title actionbar
    private static readonly Action<ServerPlayer, Component> SendActionBarText =
        (player, text) => player.Connection.Send(new ClientboundSetActionBarTextPacket(text));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("title")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("clear")
                    .Executes(c => ClearTitle((ServerCommandSource)c.GetSource(),
                        EntityArgument.GetPlayers(c, "targets"), false)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("reset")
                    .Executes(c => ClearTitle((ServerCommandSource)c.GetSource(),
                        EntityArgument.GetPlayers(c, "targets"), true)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("title")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Component>.Argument("title",
                            ComponentArgument.TextComponent())
                        .Executes(c => ShowTitle((ServerCommandSource)c.GetSource(),
                            EntityArgument.GetPlayers(c, "targets"), ComponentArgument.GetRawComponent(c, "title"),
                            "title", SendTitleText))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("subtitle")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Component>.Argument("title",
                            ComponentArgument.TextComponent())
                        .Executes(c => ShowTitle((ServerCommandSource)c.GetSource(),
                            EntityArgument.GetPlayers(c, "targets"), ComponentArgument.GetRawComponent(c, "title"),
                            "subtitle", SendSubtitleText))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("actionbar")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Component>.Argument("title",
                            ComponentArgument.TextComponent())
                        .Executes(c => ShowTitle((ServerCommandSource)c.GetSource(),
                            EntityArgument.GetPlayers(c, "targets"), ComponentArgument.GetRawComponent(c, "title"),
                            "actionbar", SendActionBarText))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("times")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("fadeIn", TimeArgument.Time())
                        .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("stay", TimeArgument.Time())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("fadeOut", TimeArgument.Time())
                                .Executes(c => SetTimes((ServerCommandSource)c.GetSource(),
                                    EntityArgument.GetPlayers(c, "targets"),
                                    IntegerArgumentType.GetInteger(c, "fadeIn"),
                                    IntegerArgumentType.GetInteger(c, "stay"),
                                    IntegerArgumentType.GetInteger(c, "fadeOut")))))))));
    }

    //ClearTitle clears the title; when resetTimes is true it also resets the animation durations, maps to vanilla clear/reset
    private static int ClearTitle(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets, bool resetTimes)
    {
        foreach (var player in targets)
            player.Connection.Send(new ClientboundClearTitlesPacket(resetTimes));
        source.SendSuccess(targets.Count == 1
            ? $"{(resetTimes ? "reset" : "cleared")} the title of {targets[0].Profile.Name}"
            : $"{(resetTimes ? "reset" : "cleared")} the title of {targets.Count} players");
        return targets.Count;
    }

    //ShowTitle sends the component to each target on the given channel, maps to vanilla showTitle
    private static int ShowTitle(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets, Component title,
        string type, Action<ServerPlayer, Component> send)
    {
        foreach (var player in targets) send(player, title);
        source.SendSuccess(targets.Count == 1
            ? $"sent {type} to {targets[0].Profile.Name}"
            : $"sent {type} to {targets.Count} players");
        return targets.Count;
    }

    //SetTimes sets the title fade-in/stay/fade-out ticks, maps to vanilla setTimes
    private static int SetTimes(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets,
        int fadeIn, int stay, int fadeOut)
    {
        foreach (var player in targets)
            player.Connection.Send(new ClientboundSetTitlesAnimationPacket(fadeIn, stay, fadeOut));
        source.SendSuccess(targets.Count == 1
            ? $"set the title animation durations of {targets[0].Profile.Name}"
            : $"set the title animation durations of {targets.Count} players");
        return targets.Count;
    }
}
