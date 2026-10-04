using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.Commands;

//SpreadPlayersCommand spreadplayers 命令对应原版 net.minecraft.server.commands.SpreadPlayersCommand
//把目标摊开在中心周围 落点反复迭代到互相不挤 再按队或逐个落到地表
//under 分支限制最高落点 不给时用当前维度的建筑高度上界
public static class SpreadPlayersCommand
{
    //MaxIterationCount 落点迭代上限 对应原版 MAX_ITERATION_COUNT
    private const int MaxIterationCount = 10000;

    private static readonly Dynamic4CommandExceptionType ErrorFailedToSpreadTeams =
        new((count, x, z, recommended) =>
            new LiteralMessage($"迭代 {MaxIterationCount} 次仍无法把 {count} 支队伍散布在 {x} {z} 附近 最小间距只有 {recommended}"));

    private static readonly Dynamic4CommandExceptionType ErrorFailedToSpreadEntities =
        new((count, x, z, recommended) =>
            new LiteralMessage($"迭代 {MaxIterationCount} 次仍无法把 {count} 个实体散布在 {x} {z} 附近 最小间距只有 {recommended}"));

    private static readonly Dynamic2CommandExceptionType ErrorInvalidMaxHeight =
        new((suppliedMaxHeight, worldMinHeight) =>
            new LiteralMessage($"最大高度 {suppliedMaxHeight} 低于世界最低高度 {worldMinHeight}"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //targets 节点两处挂载 一处用维度建筑高度上界 一处用 under 给定的最大高度
        var directTargets = RequiredArgumentBuilder<CommandSourceStack, EntitySelector>
            .Argument("targets", EntityArgument.Entities())
            .Executes(c => SpreadPlayers((ServerCommandSource)c.GetSource(), Vec2Argument.GetVec2(c, "center"),
                FloatArgumentType.GetFloat(c, "spreadDistance"), FloatArgumentType.GetFloat(c, "maxRange"),
                ((ServerCommandSource)c.GetSource()).PlayerOrThrow.Level.MaxBuildHeight,
                BoolArgumentType.GetBool(c, "respectTeams"), EntityArgument.GetEntities(c, "targets")));

        var underTargets = RequiredArgumentBuilder<CommandSourceStack, EntitySelector>
            .Argument("targets", EntityArgument.Entities())
            .Executes(c => SpreadPlayers((ServerCommandSource)c.GetSource(), Vec2Argument.GetVec2(c, "center"),
                FloatArgumentType.GetFloat(c, "spreadDistance"), FloatArgumentType.GetFloat(c, "maxRange"),
                IntegerArgumentType.GetInteger(c, "maxHeight"),
                BoolArgumentType.GetBool(c, "respectTeams"), EntityArgument.GetEntities(c, "targets")));

        var under = LiteralArgumentBuilder<CommandSourceStack>.Literal("under")
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("maxHeight", IntegerArgumentType.Integer())
                .Then(RequiredArgumentBuilder<CommandSourceStack, bool>.Argument("respectTeams", BoolArgumentType.Bool())
                    .Then(underTargets)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("spreadplayers")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("center", Vec2Argument.Vec2())
                .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("spreadDistance",
                        FloatArgumentType.FloatArg(0f))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("maxRange",
                            FloatArgumentType.FloatArg(1f))
                        .Then(RequiredArgumentBuilder<CommandSourceStack, bool>.Argument("respectTeams",
                                BoolArgumentType.Bool())
                            .Then(directTargets))
                        .Then(under)))));
    }

    //SpreadPlayers 把目标摊开 对应原版 spreadPlayers
    private static int SpreadPlayers(ServerCommandSource source, (double X, double Z) center, float spreadDistance,
        float maxRange, int maxHeight, bool respectTeams, IReadOnlyList<CommandTarget> targets)
    {
        var level = source.PlayerOrThrow.Level;
        var minY = level.MinBuildHeight;
        if (maxHeight < minY) throw ErrorInvalidMaxHeight.Create(maxHeight, minY);
        var random = RandomSource.Create();
        var minX = center.X - maxRange;
        var minZ = center.Z - maxRange;
        var maxX = center.X + maxRange;
        var maxZ = center.Z + maxRange;
        var positions = CreateInitialPositions(random,
            respectTeams ? GetNumberOfTeams(targets) : targets.Count, minX, minZ, maxX, maxZ);
        SpreadPositions(center, spreadDistance, level, random, minX, minZ, maxX, maxZ, maxHeight, positions, respectTeams);
        var distance = SetPlayerPositions(source, targets, level, positions, maxHeight, respectTeams);
        source.SendSuccess(respectTeams
            ? $"已将 {positions.Length} 支队伍散布在 {center.X:0.00} {center.Z:0.00} 附近 平均间距 {distance:0.00}"
            : $"已将 {positions.Length} 个实体散布在 {center.X:0.00} {center.Z:0.00} 附近 平均间距 {distance:0.00}");
        return positions.Length;
    }

    //GetNumberOfTeams 数目标涉及的队伍数 对应原版 getNumberOfTeams
    //队伍数据未接入 原版 getTeam 恒 null 所以全部目标同属一队
    private static int GetNumberOfTeams(IReadOnlyList<CommandTarget> targets) => targets.Count == 0 ? 0 : 1;

    //CreateInitialPositions 在范围内随机撒初始落点 对应原版 createInitialPositions
    private static Position[] CreateInitialPositions(RandomSource random, int count,
        double minX, double minZ, double maxX, double maxZ)
    {
        var result = new Position[count];
        for (var i = 0; i < result.Length; i++)
        {
            var position = new Position();
            position.Randomize(random, minX, minZ, maxX, maxZ);
            result[i] = position;
        }
        return result;
    }

    //SpreadPositions 迭代把落点推开并夹回范围 全部落点站得住才收敛 对应原版 spreadPositions
    private static void SpreadPositions((double X, double Z) center, double spreadDistance, ServerLevel level,
        RandomSource random, double minX, double minZ, double maxX, double maxZ, int maxHeight,
        Position[] positions, bool respectTeams)
    {
        var hasCollisions = true;
        //哨兵沿用原版的 float 最大值 收敛后一定比它小
        var minDistance = (double)float.MaxValue;
        var iteration = 0;
        while (iteration < MaxIterationCount && hasCollisions)
        {
            hasCollisions = false;
            minDistance = float.MaxValue;
            for (var i = 0; i < positions.Length; i++)
            {
                var position = positions[i];
                var neighbourCount = 0;
                var averageX = 0.0;
                var averageZ = 0.0;
                for (var j = 0; j < positions.Length; j++)
                {
                    if (i == j) continue;
                    var neighbour = positions[j];
                    var dist = position.Dist(neighbour);
                    minDistance = Math.Min(dist, minDistance);
                    if (dist >= spreadDistance) continue;
                    neighbourCount++;
                    averageX += neighbour.X - position.X;
                    averageZ += neighbour.Z - position.Z;
                }
                if (neighbourCount > 0)
                {
                    averageX /= neighbourCount;
                    averageZ /= neighbourCount;
                    var length = Math.Sqrt(averageX * averageX + averageZ * averageZ);
                    //邻居挤在一起就顺着平均方向往反方向挪 完全重合才重新随机
                    if (length > 0.0)
                    {
                        position.X -= averageX / length;
                        position.Z -= averageZ / length;
                    }
                    else
                    {
                        position.Randomize(random, minX, minZ, maxX, maxZ);
                    }
                    hasCollisions = true;
                }
                if (position.Clamp(minX, minZ, maxX, maxZ)) hasCollisions = true;
            }
            if (!hasCollisions)
            {
                foreach (var position in positions)
                {
                    if (position.IsSafe(level, maxHeight)) continue;
                    position.Randomize(random, minX, minZ, maxX, maxZ);
                    hasCollisions = true;
                }
            }
            iteration++;
        }
        if (minDistance == float.MaxValue) minDistance = 0.0;
        if (iteration < MaxIterationCount) return;
        throw respectTeams
            ? ErrorFailedToSpreadTeams.Create(positions.Length, center.X, center.Z, minDistance.ToString("0.00"))
            : ErrorFailedToSpreadEntities.Create(positions.Length, center.X, center.Z, minDistance.ToString("0.00"));
    }

    //SetPlayerPositions 把目标落到各自落点上并回算平均间距 对应原版 setPlayerPositions
    private static double SetPlayerPositions(ServerCommandSource source, IReadOnlyList<CommandTarget> targets,
        ServerLevel level, Position[] positions, int maxHeight, bool respectTeams)
    {
        var averageDistance = 0.0;
        var positionIndex = 0;
        Position? teamPosition = null;
        foreach (var target in targets)
        {
            Position position;
            if (respectTeams)
            {
                //队伍数据未接入 所有目标同属一队 只取首个落点 等价原版全无队伍时的映射
                teamPosition ??= positions[positionIndex++];
                position = teamPosition;
            }
            else
            {
                position = positions[positionIndex++];
            }
            TeleportCommand.TeleportTarget(source, target, Mth.Floor(position.X) + 0.5,
                position.GetSpawnY(level, maxHeight), Mth.Floor(position.Z) + 0.5);
            var closest = double.MaxValue;
            foreach (var testPosition in positions)
            {
                if (ReferenceEquals(position, testPosition)) continue;
                closest = Math.Min(position.Dist(testPosition), closest);
            }
            averageDistance += closest;
        }
        return targets.Count < 2 ? 0.0 : averageDistance / targets.Count;
    }

    //Position 摊开算法里的二维落点 对应原版 SpreadPlayersCommand$Position
    private sealed class Position
    {
        public double X;
        public double Z;

        //Dist 两落点水平距离 对应原版 dist
        public double Dist(Position other)
        {
            var dx = X - other.X;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        //Clamp 夹回范围内 有改动返回真 对应原版 clamp
        public bool Clamp(double minX, double minZ, double maxX, double maxZ)
        {
            var changed = false;
            if (X < minX)
            {
                X = minX;
                changed = true;
            }
            else if (X > maxX)
            {
                X = maxX;
                changed = true;
            }
            if (Z < minZ)
            {
                Z = minZ;
                changed = true;
            }
            else if (Z > maxZ)
            {
                Z = maxZ;
                changed = true;
            }
            return changed;
        }

        //GetSpawnY 从最高点往下找第一处能站的三格空腔 找不到返回最高点上界 对应原版 getSpawnY
        public int GetSpawnY(ServerLevel level, int maxHeight)
        {
            var x = Mth.Floor(X);
            var z = Mth.Floor(Z);
            var y = maxHeight + 1;
            var air2Above = IsAir(level.GetBlockState(new BlockPos(x, y, z)));
            y--;
            var belowIsAir = IsAir(level.GetBlockState(new BlockPos(x, y, z)));
            while (true)
            {
                var air1Above = belowIsAir;
                if (y <= level.MinBuildHeight) return maxHeight + 1;
                y--;
                var currentIsAir = IsAir(level.GetBlockState(new BlockPos(x, y, z)));
                if (!currentIsAir && air1Above && air2Above) return y + 1;
                air2Above = air1Above;
                belowIsAir = currentIsAir;
            }
        }

        //IsSafe 落点脚下方块不是液体也不着火才算站得住 对应原版 isSafe
        public bool IsSafe(ServerLevel level, int maxHeight)
        {
            var spawnY = GetSpawnY(level, maxHeight) - 1;
            if (spawnY >= maxHeight) return false;
            var state = level.GetBlockState(new BlockPos(Mth.Floor(X), spawnY, Mth.Floor(Z)));
            if (state is not { } value) return true;
            return !IsLiquid(value) && !IsFire(value);
        }

        //Randomize 在范围内重新随机 对应原版 randomize
        public void Randomize(RandomSource random, double minX, double minZ, double maxX, double maxZ)
        {
            X = Mth.NextDouble(random, minX, maxX);
            Z = Mth.NextDouble(random, minZ, maxZ);
        }
    }

    //IsAir 空气判定 区块未加载时按空气看待 对应原版 BlockState.isAir
    private static bool IsAir(BlockState? state)
        => state is not { } value || value.Owner is BlockBehaviour { IsAir: true };

    //IsLiquid 液体判定 流体状态非空即水或岩浆 对应原版 BlockState.liquid
    private static bool IsLiquid(BlockState state) => !state.FluidState.IsEmpty;

    //IsFire 火焰判定 原版走 BlockTags.FIRE 标签 NC 火焰类只有火与灵魂火两个方块
    private static bool IsFire(BlockState state) => state.Owner is Blocks.BaseFireBlock;
}
