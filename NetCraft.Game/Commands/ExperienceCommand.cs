using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//ExperienceCommand experience command, maps to vanilla net.minecraft.server.commands.ExperienceCommand
//The add/set/query actions act on a player's xp and level
//Vanilla add goes through giveExperiencePoints and also does increaseScore for the scoreboard; nc has no scoreboard so that side effect is omitted
public static class ExperienceCommand
{
    //Xp measurement dimension: points = xp points within the current level, levels = level
    private enum Kind
    {
        Points,
        Levels,
    }

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("experience")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("target", EntityArgument.Players())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("amount", IntegerArgumentType.Integer())
                        .Executes(context => Add(context, Kind.Points))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("points")
                            .Executes(context => Add(context, Kind.Points)))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("levels")
                            .Executes(context => Add(context, Kind.Levels))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("set")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("target", EntityArgument.Players())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("amount", IntegerArgumentType.Integer(0))
                        .Executes(context => Set(context, Kind.Points))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("points")
                            .Executes(context => Set(context, Kind.Points)))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("levels")
                            .Executes(context => Set(context, Kind.Levels))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("target", EntityArgument.Player())
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("points")
                        .Executes(context => Query(context, Kind.Points)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("levels")
                        .Executes(context => Query(context, Kind.Levels))))));
    }

    //XpNeededForNextLevel xp points needed to reach the next level, maps to vanilla Player.getXpNeededForNextLevel's three-segment formula
    private static int XpNeededForNextLevel(int level)
        => level >= 30 ? 112 + (level - 30) * 9
        : level >= 15 ? 37 + (level - 15) * 5
        : 7 + level * 2;

    //Add adds xp by dimension; re-sends the xp packet after, or the client sees no change
    private static int Add(CommandContext<CommandSourceStack> context, Kind kind)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var players = EntityArgument.GetPlayers(context, "target");
        var amount = IntegerArgumentType.GetInteger(context, "amount");
        foreach (var player in players)
        {
            if (kind == Kind.Levels) player.XpLevel += amount;
            else GiveExperiencePoints(player, amount);
            SyncExperience(player);
        }

        if (players.Count == 1)
            source.SendSuccess($"gave {players[0].Profile.Name} {amount} {(kind == Kind.Levels ? "levels" : "xp")}");
        else
            source.SendSuccess($"gave {players.Count} players {amount} {(kind == Kind.Levels ? "levels" : "xp")}");
        return players.Count;
    }

    //Set sets xp by dimension; when setting points, reaching the level-up threshold fails like vanilla
    private static int Set(CommandContext<CommandSourceStack> context, Kind kind)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var players = EntityArgument.GetPlayers(context, "target");
        var amount = IntegerArgumentType.GetInteger(context, "amount");
        var success = 0;
        foreach (var player in players)
        {
            if (kind == Kind.Levels)
            {
                player.XpLevel = amount;
            }
            else
            {
                var needed = XpNeededForNextLevel(player.XpLevel);
                if (amount >= needed) continue;
                //Vanilla setExperiencePoints clamps the progress to 0..(f-1)/f to keep it below the level
                player.XpProgress = Math.Clamp((float)amount / needed, 0f, (needed - 1f) / needed);
            }
            success++;
            SyncExperience(player);
        }

        if (success == 0)
        {
            source.SendFailure("xp points cannot reach the level-up threshold");
            return 0;
        }

        if (players.Count == 1)
            source.SendSuccess($"set {players[0].Profile.Name}'s {kind switch { Kind.Levels => "level", _ => "xp" }} to {amount}");
        else
            source.SendSuccess($"set {players.Count} players' {kind switch { Kind.Levels => "level", _ => "xp" }} to {amount}");
        return success;
    }

    //Query reads xp back; points are the progress times the level-up requirement rounded, maps to vanilla queryExperience
    private static int Query(CommandContext<CommandSourceStack> context, Kind kind)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetPlayer(context, "target");
        var result = kind == Kind.Levels
            ? target.XpLevel
            : (int)Math.Floor(target.XpProgress * XpNeededForNextLevel(target.XpLevel));
        source.SendSuccess($"{target.Profile.Name}'s {(kind == Kind.Levels ? "level" : "xp")} is {result}");
        return result;
    }

    //GiveExperiencePoints gives total xp like vanilla and handles level carry-over, maps to vanilla Player.giveExperiencePoints
    private static void GiveExperiencePoints(ServerPlayer player, int amount)
    {
        player.XpProgress += (float)amount / XpNeededForNextLevel(player.XpLevel);
        player.XpTotal = Math.Clamp(player.XpTotal + amount, 0, int.MaxValue);
        while (player.XpProgress < 0f)
        {
            player.XpProgress *= XpNeededForNextLevel(player.XpLevel);
            if (player.XpLevel > 0)
            {
                player.XpLevel--;
                player.XpProgress = 1f + player.XpProgress / XpNeededForNextLevel(player.XpLevel);
            }
            else
            {
                player.XpProgress = 0f;
            }
        }
        while (player.XpProgress >= 1f)
        {
            player.XpProgress = (player.XpProgress - 1f) * XpNeededForNextLevel(player.XpLevel);
            player.XpLevel++;
            player.XpProgress /= XpNeededForNextLevel(player.XpLevel);
        }
    }

    //SyncExperience re-sends a packet after xp changes, consistent with vanilla syncing on xp change
    private static void SyncExperience(ServerPlayer player)
        => player.Connection.Send(new ClientboundSetExperiencePacket(player.XpProgress, player.XpTotal, player.XpLevel));
}
