using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//SeedCommand seed command, maps to vanilla net.minecraft.server.commands.SeedCommand
//Reports the world seed fixed in the save
public static class SeedCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("seed")
            .Requires(s => s.HasPermission(2))
            .Executes(context =>
            {
                var source = (ServerCommandSource)context.GetSource();
                source.SendSuccess($"the world seed is {source.Server.WorldSeed}");
                return 1;
            }));
    }
}
