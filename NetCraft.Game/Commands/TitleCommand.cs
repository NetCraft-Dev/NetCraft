using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Commands;

//TitleCommand title 命令对应原版 net.minecraft.server.commands.TitleCommand
//clear/reset/title/subtitle/actionbar/times 六支 目标为多玩家
//组件不做选择器解析 对应原版 getRawComponent 而非 getResolvedComponent
public static class TitleCommand
{
    //SendTitleText 标题正文 显示在屏幕中央
    private static readonly Action<ServerPlayer, Component> SendTitleText =
        (player, text) => player.Connection.Send(new ClientboundSetTitleTextPacket(text));

    //SendSubtitleText 副标题 显示在标题下方
    private static readonly Action<ServerPlayer, Component> SendSubtitleText =
        (player, text) => player.Connection.Send(new ClientboundSetSubtitleTextPacket(text));

    //SendActionBarText 动作栏文字 走专用包与原版 title actionbar 一致
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
                            "标题", SendTitleText))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("subtitle")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Component>.Argument("title",
                            ComponentArgument.TextComponent())
                        .Executes(c => ShowTitle((ServerCommandSource)c.GetSource(),
                            EntityArgument.GetPlayers(c, "targets"), ComponentArgument.GetRawComponent(c, "title"),
                            "副标题", SendSubtitleText))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("actionbar")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Component>.Argument("title",
                            ComponentArgument.TextComponent())
                        .Executes(c => ShowTitle((ServerCommandSource)c.GetSource(),
                            EntityArgument.GetPlayers(c, "targets"), ComponentArgument.GetRawComponent(c, "title"),
                            "动作栏", SendActionBarText))))
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

    //ClearTitle 清除标题 resetTimes 为真时顺带把动画时长复位 对应原版 clear/reset 两支
    private static int ClearTitle(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets, bool resetTimes)
    {
        foreach (var player in targets)
            player.Connection.Send(new ClientboundClearTitlesPacket(resetTimes));
        source.SendSuccess(targets.Count == 1
            ? $"已{(resetTimes ? "重置" : "清除")} {targets[0].Profile.Name} 的标题"
            : $"已{(resetTimes ? "重置" : "清除")} {targets.Count} 个玩家的标题");
        return targets.Count;
    }

    //ShowTitle 把组件按指定通道发给每个目标 对应原版 showTitle
    private static int ShowTitle(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets, Component title,
        string type, Action<ServerPlayer, Component> send)
    {
        foreach (var player in targets) send(player, title);
        source.SendSuccess(targets.Count == 1
            ? $"已向 {targets[0].Profile.Name} 发送{type}"
            : $"已向 {targets.Count} 个玩家发送{type}");
        return targets.Count;
    }

    //SetTimes 设置标题淡入停留淡出刻数 对应原版 setTimes
    private static int SetTimes(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets,
        int fadeIn, int stay, int fadeOut)
    {
        foreach (var player in targets)
            player.Connection.Send(new ClientboundSetTitlesAnimationPacket(fadeIn, stay, fadeOut));
        source.SendSuccess(targets.Count == 1
            ? $"已设置 {targets[0].Profile.Name} 的标题动画时长"
            : $"已设置 {targets.Count} 个玩家的标题动画时长");
        return targets.Count;
    }
}
