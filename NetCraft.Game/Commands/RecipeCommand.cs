using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//RecipeCommand recipe command, maps to vanilla net.minecraft.server.commands.RecipeCommand
//Vanilla give/take go through ServerRecipeBook (awardRecipes/resetRecipes) to operate the player's recipe book
//NC has no recipe book subsystem yet (no player recipe records, the recipe book packets are placeholders), so both branches keep only the argument tree and the reply is not implemented
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

    //Unsupported the recipe book subsystem is absent, both actions reply unsupported
    private static int Unsupported(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        source.SendFailure("NC has no recipe book subsystem yet; /recipe is not implemented");
        return 0;
    }
}
