using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//SpectateCommand spectate 命令对应原版 net.minecraft.server.commands.SpectateCommand
//<target> [<player>] 让旁观模式玩家把视角切到目标实体 无参数时把视角收回自身
public static class SpectateCommand
{
    //ErrorSelf 不能旁观自己 对应原版 ERROR_SELF
    private static readonly SimpleCommandExceptionType ErrorSelf =
        new(new LiteralMessage("不能旁观自己"));

    //ErrorNotSpectator 指定玩家不在旁观模式 对应原版 ERROR_NOT_SPECTATOR
    private static readonly DynamicCommandExceptionType ErrorNotSpectator =
        new(name => new LiteralMessage($"{name} 不在旁观模式"));

    //ErrorCannotSpectate 目标类型不可被旁观 对应原版 ERROR_CANNOT_SPECTATE
    private static readonly DynamicCommandExceptionType ErrorCannotSpectate =
        new(name => new LiteralMessage($"无法旁观 {name}"));

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

    //Spectate 切换目标玩家的旁观相机 对应原版 SpectateCommand.spectate
    //target 为空表示收回自身视角 校验顺序与报错同原版
    private static int Spectate(ServerCommandSource source, CommandTarget? target, ServerPlayer player)
    {
        if (target is not null && target.EntityId == player.EntityId)
            throw ErrorSelf.Create();
        if (!player.IsSpectator)
            throw ErrorNotSpectator.Create(player.Profile.Name);
        //追踪视距为 0 的实体不下发 AddEntity 客户端拿不到 无法旁观 对应原版 clientTrackingRange
        if (target?.Type is { TrackingRangeChunks: 0 })
            throw ErrorCannotSpectate.Create(target.Name);
        player.SetCamera(target?.Player ?? (ITrackedEntity?)target?.WorldEntity);
        if (target is not null)
            source.SendSuccess($"正在旁观 {target.Name}");
        else
            source.SendSuccess("已停止旁观");
        return 1;
    }
}
