using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//RecipeCommand recipe 命令对应原版 net.minecraft.server.commands.RecipeCommand
//原版 give/take 走 ServerRecipeBook(awardRecipes/resetRecipes) 操作玩家配方书
//NC 尚无配方书子系统(无玩家配方记录 配方书协议包也仅占位) 故两分支只保留参数树 执行回执未实现
public static class RecipeCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("recipe")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("give")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("recipe",
                            new ResourceKeyArgument(Registries.RECIPE.Identifier))
                        .Executes(Unsupported))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("*")
                        .Executes(Unsupported))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("take")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("recipe",
                            new ResourceKeyArgument(Registries.RECIPE.Identifier))
                        .Executes(Unsupported))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("*")
                        .Executes(Unsupported)))));
    }

    //Unsupported 配方书子系统缺位 两动作统一回执不支持
    private static int Unsupported(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        source.SendFailure("NC 尚无配方书子系统 /recipe 暂未实现");
        return 0;
    }
}
