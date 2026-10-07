using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//ForceLoadCommand forceload command, maps to vanilla net.minecraft.server.commands.ForceLoadCommand
//Three branches add/remove/query; at most 256 chunks per call; after the ticket is placed the chunk source writes it into chunk_tickets.dat
public static class ForceLoadCommand
{
    //MaxChunkLimit the max chunks changeable per call, maps to vanilla MAX_CHUNK_LIMIT
    private const int MaxChunkLimit = 256;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("forceload")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                .Then(RequiredArgumentBuilder<CommandSourceStack, ColumnCoordinates>.Argument(
                        "from", ColumnPosArgument.ColumnPos())
                    .Executes(c => Change((ServerCommandSource)c.GetSource(),
                        ColumnPosArgument.GetColumn(c, "from"), ColumnPosArgument.GetColumn(c, "from"), true))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, ColumnCoordinates>.Argument(
                            "to", ColumnPosArgument.ColumnPos())
                        .Executes(c => Change((ServerCommandSource)c.GetSource(),
                            ColumnPosArgument.GetColumn(c, "from"), ColumnPosArgument.GetColumn(c, "to"), true)))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                .Then(RequiredArgumentBuilder<CommandSourceStack, ColumnCoordinates>.Argument(
                        "from", ColumnPosArgument.ColumnPos())
                    .Executes(c => Change((ServerCommandSource)c.GetSource(),
                        ColumnPosArgument.GetColumn(c, "from"), ColumnPosArgument.GetColumn(c, "from"), false))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, ColumnCoordinates>.Argument(
                            "to", ColumnPosArgument.ColumnPos())
                        .Executes(c => Change((ServerCommandSource)c.GetSource(),
                            ColumnPosArgument.GetColumn(c, "from"), ColumnPosArgument.GetColumn(c, "to"), false))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("all")
                    .Executes(c => RemoveAll((ServerCommandSource)c.GetSource()))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Executes(c => ListForceLoad((ServerCommandSource)c.GetSource()))
                .Then(RequiredArgumentBuilder<CommandSourceStack, ColumnCoordinates>.Argument(
                        "pos", ColumnPosArgument.ColumnPos())
                    .Executes(c => Query((ServerCommandSource)c.GetSource(), ColumnPosArgument.GetColumn(c, "pos"))))));
    }

    //Change toggles forced loading in a batch, maps to vanilla changeForceLoad
    //Coordinates are given in blocks, converted to chunks, then a ticket is placed per chunk; returns the number of chunks actually changed
    private static int Change(ServerCommandSource source, (int X, int Z) from, (int X, int Z) to, bool add)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("the current dimension does not support forced loading");
            return 0;
        }
        var minX = Math.Min(from.X, to.X);
        var minZ = Math.Min(from.Z, to.Z);
        var maxX = Math.Max(from.X, to.X);
        var maxZ = Math.Max(from.Z, to.Z);
        if (minX < -30000000 || minZ < -30000000 || maxX >= 30000000 || maxZ >= 30000000)
        {
            source.SendFailure("the coordinate is out of the world bounds");
            return 0;
        }
        var minChunkX = minX >> 4;
        var minChunkZ = minZ >> 4;
        var maxChunkX = maxX >> 4;
        var maxChunkZ = maxZ >> 4;
        long count = (long)(maxChunkX - minChunkX + 1) * (maxChunkZ - minChunkZ + 1);
        if (count > MaxChunkLimit)
        {
            source.SendFailure($"at most {MaxChunkLimit} chunks can be force loaded at once; {count} selected");
            return 0;
        }
        ChunkPos? first = null;
        var changed = 0;
        for (var x = minChunkX; x <= maxChunkX; x++)
            for (var z = minChunkZ; z <= maxChunkZ; z++)
            {
                if (!level.SetChunkForced(x, z, add)) continue;
                changed++;
                first ??= new ChunkPos(x, z);
            }
        if (changed == 0)
        {
            source.SendFailure(add ? "these chunks are already in the force load list" : "these chunks are not in the force load list");
            return 0;
        }
        var action = add ? "force loaded" : "unforce loaded";
        if (changed == 1) source.SendSuccess($"{action} chunk {first}");
        else source.SendSuccess($"{action} chunks {new ChunkPos(minChunkX, minChunkZ)} to {new ChunkPos(maxChunkX, maxChunkZ)}, {changed} total");
        return changed;
    }

    //RemoveAll removes all forced loading in the current dimension, maps to vanilla removeAll
    private static int RemoveAll(ServerCommandSource source)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("the current dimension does not support forced loading");
            return 0;
        }
        //The set is rebuilt while removing tickets; copy it before iterating
        foreach (var packed in level.GetForceLoadedChunks().ToList())
            level.SetChunkForced(ChunkPos.GetX(packed), ChunkPos.GetZ(packed), false);
        source.SendSuccess("removed all forced loading in the current dimension");
        return 0;
    }

    //ListForceLoad lists forced-loaded chunks in the current dimension, maps to vanilla listForceLoad
    private static int ListForceLoad(ServerCommandSource source)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("the current dimension does not support forced loading");
            return 0;
        }
        var chunks = level.GetForceLoadedChunks();
        if (chunks.Count == 0)
        {
            source.SendFailure("the current dimension has no force-loaded chunks");
            return 0;
        }
        var list = string.Join(", ", chunks.OrderBy(v => v).Select(v => ChunkPos.Unpack(v).ToString()));
        source.SendSuccess($"{chunks.Count} force-loaded chunks {list}");
        return chunks.Count;
    }

    //Query queries whether a single chunk is force loaded, maps to vanilla queryForceLoad
    private static int Query(ServerCommandSource source, (int X, int Z) pos)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("the current dimension does not support forced loading");
            return 0;
        }
        var chunk = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        if (level.GetForceLoadedChunks().Contains(chunk.Pack()))
        {
            source.SendSuccess($"chunk {chunk} is force loaded");
            return 1;
        }
        source.SendFailure($"chunk {chunk} is not in the force load list");
        return 0;
    }
}
