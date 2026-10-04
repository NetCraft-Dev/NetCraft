using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//StopSoundCommand stopsound 命令对应原版 net.minecraft.server.commands.StopSoundCommand
//停止目标身上正在播放的音效 省略音源时停全部音源
public static class StopSoundCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("stopsound")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Executes(context => Stop(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("source", StringArgumentType.Word())
                    .Executes(context => Stop(context, ParseSource(context))))));
    }

    //ParseSource 词参数转音源枚举 名字不匹配按全部音源处理
    private static SoundSource? ParseSource(CommandContext<CommandSourceStack> context)
    {
        var name = StringArgumentType.GetString(context, "source");
        return Enum.TryParse<SoundSource>(name, true, out var parsed) ? parsed : null;
    }

    //Stop 发停止音效包 音源为 null 时停该玩家所有音源
    private static int Stop(CommandContext<CommandSourceStack> context, SoundSource? source)
    {
        if (context.GetSource() is not ServerCommandSource source2) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        if (targets.Count == 0)
        {
            source2.SendFailure("没有找到匹配的玩家");
            return 0;
        }

        foreach (var target in targets)
            target.Connection.Send(new ClientboundStopSoundPacket(null, source));

        source2.SendSuccess(source is null
            ? $"已停止 {targets.Count} 名玩家的全部音效"
            : $"已停止 {targets.Count} 名玩家的 {source} 音源");
        return targets.Count;
    }
}
