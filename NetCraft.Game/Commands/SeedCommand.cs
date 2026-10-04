using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//SeedCommand seed 命令对应原版 net.minecraft.server.commands.SeedCommand
//回执本世界固化在存档里的随机种子
public static class SeedCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("seed")
            .Requires(s => s.HasPermission(2))
            .Executes(context =>
            {
                var source = (ServerCommandSource)context.GetSource();
                source.SendSuccess($"世界种子为 {source.Server.WorldSeed}");
                return 1;
            }));
    }
}
