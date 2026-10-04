using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//PlaySoundCommand playsound 命令对应原版 net.minecraft.server.commands.PlaySoundCommand
//在目标位置播放指定音效 原版的 source 位置音量渐隐分支暂缺只保留音效与目标
public static class PlaySoundCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("playsound")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("sound", StringArgumentType.String())
                .Executes(context => Play(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Executes(context => Play(context, EntityArgument.GetPlayers(context, "targets"))))));
    }

    //Play 逐目标发播放音效包 省略目标时只放给执行者
    private static int Play(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer>? explicitTargets)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var raw = StringArgumentType.GetString(context, "sound");
        if (Identifier.TryParse(raw) is not { } id)
        {
            source.SendFailure($"音效标识符非法 {raw}");
            return 0;
        }

        var targets = explicitTargets ?? new[] { source.Player };
        var sound = new SoundEvent(id);
        var played = 0;
        foreach (var target in targets)
        {
            target.Connection.Send(new ClientboundSoundPacket(sound, SoundSource.Master,
                target.Position.X, target.Position.Y, target.Position.Z, 1f, 1f, Random.Shared.NextInt64()));
            played++;
        }

        source.SendSuccess($"已为 {played} 名玩家播放音效 {id}");
        return played;
    }
}
