using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Level;

namespace NetCraft.Game.Commands;

//DifficultyCommand difficulty command, maps to vanilla net.minecraft.server.commands.DifficultyCommand
//Rewrites the save difficulty and broadcasts the change packet; vanilla uses a dedicated DifficultyArgument, this uses a word argument with manual validation
public static class DifficultyCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("difficulty")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("difficulty", StringArgumentType.Word())
                .Executes(Set)));
    }

    //Set sends no packet when the difficulty is unchanged, consistent with vanilla
    private static int Set(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "difficulty");
        var difficulty = Difficulty.ByName(name);
        if (difficulty is null)
        {
            source.SendFailure($"unknown difficulty {name}; options are peaceful easy normal hard");
            return 0;
        }

        var levelData = source.Server.LevelData;
        if (levelData.DifficultyName.Equals(difficulty.Name, StringComparison.OrdinalIgnoreCase))
        {
            source.SendFailure($"the difficulty is already {difficulty.Name}");
            return 0;
        }

        levelData.DifficultyName = difficulty.Name;
        source.Server.Settings.SetDifficulty(difficulty.Name);
        source.Server.Settings.SaveCurrent();
        source.Server.PlayerList.BroadcastAll(
            new ClientboundChangeDifficultyPacket(difficulty, levelData.DifficultyLocked));
        source.SendSuccess($"difficulty set to {difficulty.Name}");
        return 1;
    }
}
