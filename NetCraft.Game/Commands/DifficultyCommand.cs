using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Level;

namespace NetCraft.Game.Commands;

//DifficultyCommand difficulty 命令对应原版 net.minecraft.server.commands.DifficultyCommand
//改写存档难度并广播变更包 原版用专用 DifficultyArgument 这里用词参数加手工校验
public static class DifficultyCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("difficulty")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("difficulty", StringArgumentType.Word())
                .Executes(Set)));
    }

    //Set 难度未变时不发包 与原版一致
    private static int Set(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var name = StringArgumentType.GetString(context, "difficulty");
        var difficulty = Difficulty.ByName(name);
        if (difficulty is null)
        {
            source.SendFailure($"未知难度 {name} 可选 peaceful easy normal hard");
            return 0;
        }

        var levelData = source.Server.LevelData;
        if (levelData.DifficultyName.Equals(difficulty.Name, StringComparison.OrdinalIgnoreCase))
        {
            source.SendFailure($"难度已经是 {difficulty.Name}");
            return 0;
        }

        levelData.DifficultyName = difficulty.Name;
        source.Server.Settings.SetDifficulty(difficulty.Name);
        source.Server.Settings.SaveCurrent();
        source.Server.PlayerList.BroadcastAll(
            new ClientboundChangeDifficultyPacket(difficulty, levelData.DifficultyLocked));
        source.SendSuccess($"难度已设为 {difficulty.Name}");
        return 1;
    }
}
