using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//ExperienceCommand experience 命令对应原版 net.minecraft.server.commands.ExperienceCommand
//add/set/query 三个动作作用于玩家的经验值与等级
//原版 add 走 giveExperiencePoints 会顺带 increaseScore 记分板加分 nc 无记分板故省略该副作用
public static class ExperienceCommand
{
    //经验计量维度 points=当前等级内经验点数 levels=等级
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

    //XpNeededForNextLevel 升到下一级还需的经验点数 对应原版 Player.getXpNeededForNextLevel 的三段公式
    private static int XpNeededForNextLevel(int level)
        => level >= 30 ? 112 + (level - 30) * 9
        : level >= 15 ? 37 + (level - 15) * 5
        : 7 + level * 2;

    //Add 按维度增加经验 改完补发经验包否则客户端看不到变化
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
            source.SendSuccess($"已为 {players[0].Profile.Name} 增加 {amount} {(kind == Kind.Levels ? "等级" : "经验点")}");
        else
            source.SendSuccess($"已为 {players.Count} 名玩家增加 {amount} {(kind == Kind.Levels ? "等级" : "经验点")}");
        return players.Count;
    }

    //Set 按维度设置经验 设点数时达到升级阈值按原版判失败
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
                //原版 setExperiencePoints 把进度钳到 0..(f-1)/f 保证不满级
                player.XpProgress = Math.Clamp((float)amount / needed, 0f, (needed - 1f) / needed);
            }
            success++;
            SyncExperience(player);
        }

        if (success == 0)
        {
            source.SendFailure("经验点数不能达到升级阈值");
            return 0;
        }

        if (players.Count == 1)
            source.SendSuccess($"已将 {players[0].Profile.Name} 的{kind switch { Kind.Levels => "等级", _ => "经验点" }}设为 {amount}");
        else
            source.SendSuccess($"已将 {players.Count} 名玩家的{kind switch { Kind.Levels => "等级", _ => "经验点" }}设为 {amount}");
        return success;
    }

    //Query 回读经验 点数按进度乘升级需求取整 对应原版 queryExperience
    private static int Query(CommandContext<CommandSourceStack> context, Kind kind)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var target = EntityArgument.GetPlayer(context, "target");
        var result = kind == Kind.Levels
            ? target.XpLevel
            : (int)Math.Floor(target.XpProgress * XpNeededForNextLevel(target.XpLevel));
        source.SendSuccess($"{target.Profile.Name} 的{(kind == Kind.Levels ? "等级" : "经验点")}为 {result}");
        return result;
    }

    //GiveExperiencePoints 按原版给总经验并处理跨级结转 对应原版 Player.giveExperiencePoints
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

    //SyncExperience 改完经验补发一包 与原版经验变更即同步一致
    private static void SyncExperience(ServerPlayer player)
        => player.Connection.Send(new ClientboundSetExperiencePacket(player.XpProgress, player.XpTotal, player.XpLevel));
}
