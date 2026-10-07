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

//SpreadPlayersCommand spreadplayers command, maps to vanilla net.minecraft.server.commands.SpreadPlayersCommand
//Spreads the targets around a center, iterating the landing points until they no longer crowd each other, then lands them per team or one by one on the surface
//The under branch limits the maximum landing point; without it the current dimension's build height ceiling is used
public static class SpreadPlayersCommand
{
    //MaxIterationCount landing point iteration limit, maps to vanilla MAX_ITERATION_COUNT
    private const int MaxIterationCount = 10000;

    private static readonly Dynamic4CommandExceptionType ErrorFailedToSpreadTeams =
        new((count, x, z, recommended) =>
            new LiteralMessage($"after {MaxIterationCount} iterations {count} teams still cannot be spread near {x} {z}; the minimum spacing is only {recommended}"));

    private static readonly Dynamic4CommandExceptionType ErrorFailedToSpreadEntities =
        new((count, x, z, recommended) =>
            new LiteralMessage($"after {MaxIterationCount} iterations {count} entities still cannot be spread near {x} {z}; the minimum spacing is only {recommended}"));

    private static readonly Dynamic2CommandExceptionType ErrorInvalidMaxHeight =
        new((suppliedMaxHeight, worldMinHeight) =>
            new LiteralMessage($"the maximum height {suppliedMaxHeight} is below the world minimum height {worldMinHeight}"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //The targets node is attached in two places: one using the dimension build height ceiling, one using the under-given maximum height
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

    //SpreadPlayers spreads the targets, maps to vanilla spreadPlayers
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
            ? $"spread {positions.Length} teams near {center.X:0.00} {center.Z:0.00}, average spacing {distance:0.00}"
            : $"spread {positions.Length} entities near {center.X:0.00} {center.Z:0.00}, average spacing {distance:0.00}");
        return positions.Length;
    }

    //GetNumberOfTeams counts the teams involved, maps to vanilla getNumberOfTeams
    //Team data is not wired up; vanilla getTeam is always null so all targets belong to one team
    private static int GetNumberOfTeams(IReadOnlyList<CommandTarget> targets) => targets.Count == 0 ? 0 : 1;

    //CreateInitialPositions scatters initial landing points randomly in the range, maps to vanilla createInitialPositions
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

    //SpreadPositions iteratively pushes landing points apart and clamps them back; it converges only when all landing points can stand, maps to vanilla spreadPositions
    private static void SpreadPositions((double X, double Z) center, double spreadDistance, ServerLevel level,
        RandomSource random, double minX, double minZ, double maxX, double maxZ, int maxHeight,
        Position[] positions, bool respectTeams)
    {
        var hasCollisions = true;
        //The sentinel follows vanilla's float maximum; after convergence it is certainly smaller
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
                    //When neighbors crowd together they shift in the opposite direction along the average; a full overlap triggers a re-randomize
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

    //SetPlayerPositions lands the targets on their landing points and computes the average spacing, maps to vanilla setPlayerPositions
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
                //Team data is not wired up; all targets belong to one team, so only the first landing point is taken, equivalent to vanilla's mapping with no teams
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

    //Position the 2D landing point in the spread algorithm, maps to vanilla SpreadPlayersCommand$Position
    private sealed class Position
    {
        public double X;
        public double Z;

        //Dist horizontal distance between two landing points, maps to vanilla dist
        public double Dist(Position other)
        {
            var dx = X - other.X;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        //Clamp clamps back into the range; returns true when changed, maps to vanilla clamp
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

        //GetSpawnY searches downward from the highest point for the first standable three-block cavity; without one it returns the height ceiling, maps to vanilla getSpawnY
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

        //IsSafe a landing point stands only when the block underfoot is neither liquid nor fire, maps to vanilla isSafe
        public bool IsSafe(ServerLevel level, int maxHeight)
        {
            var spawnY = GetSpawnY(level, maxHeight) - 1;
            if (spawnY >= maxHeight) return false;
            var state = level.GetBlockState(new BlockPos(Mth.Floor(X), spawnY, Mth.Floor(Z)));
            if (state is not { } value) return true;
            return !IsLiquid(value) && !IsFire(value);
        }

        //Randomize re-randomizes in the range, maps to vanilla randomize
        public void Randomize(RandomSource random, double minX, double minZ, double maxX, double maxZ)
        {
            X = Mth.NextDouble(random, minX, maxX);
            Z = Mth.NextDouble(random, minZ, maxZ);
        }
    }

    //IsAir air test; an unloaded chunk is treated as air, maps to vanilla BlockState.isAir
    private static bool IsAir(BlockState? state)
        => state is not { } value || value.Owner is BlockBehaviour { IsAir: true };

    //IsLiquid liquid test; a non-empty fluid state is water or lava, maps to vanilla BlockState.liquid
    private static bool IsLiquid(BlockState state) => !state.FluidState.IsEmpty;

    //IsFire fire test; vanilla goes through the BlockTags.FIRE tag; NC's fire category only has fire and soul fire
    private static bool IsFire(BlockState state) => state.Owner is Blocks.BaseFireBlock;
}
