using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//ForceLoadCommand forceload 命令对应原版 net.minecraft.server.commands.ForceLoadCommand
//add/remove/query 三支 单次最多改动 256 个区块 落票后由区块源写进 chunk_tickets.dat
public static class ForceLoadCommand
{
    //MaxChunkLimit 单次可改动的区块上限对应原版 MAX_CHUNK_LIMIT
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

    //Change 批量开关强制加载 对应原版 changeForceLoad
    //坐标按方块给出 换算成区块后逐块落票 返回真正改变的区块数
    private static int Change(ServerCommandSource source, (int X, int Z) from, (int X, int Z) to, bool add)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("当前维度不支持强制加载");
            return 0;
        }
        var minX = Math.Min(from.X, to.X);
        var minZ = Math.Min(from.Z, to.Z);
        var maxX = Math.Max(from.X, to.X);
        var maxZ = Math.Max(from.Z, to.Z);
        if (minX < -30000000 || minZ < -30000000 || maxX >= 30000000 || maxZ >= 30000000)
        {
            source.SendFailure("坐标超出世界范围");
            return 0;
        }
        var minChunkX = minX >> 4;
        var minChunkZ = minZ >> 4;
        var maxChunkX = maxX >> 4;
        var maxChunkZ = maxZ >> 4;
        long count = (long)(maxChunkX - minChunkX + 1) * (maxChunkZ - minChunkZ + 1);
        if (count > MaxChunkLimit)
        {
            source.SendFailure($"一次最多强制加载 {MaxChunkLimit} 个区块 当前选中 {count} 个");
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
            source.SendFailure(add ? "这些区块都已在强制加载列表里" : "这些区块本来就不在强制加载列表里");
            return 0;
        }
        var action = add ? "已强制加载" : "已取消强制加载";
        if (changed == 1) source.SendSuccess($"{action}区块 {first}");
        else source.SendSuccess($"{action}区块 {new ChunkPos(minChunkX, minChunkZ)} 到 {new ChunkPos(maxChunkX, maxChunkZ)} 共 {changed} 个");
        return changed;
    }

    //RemoveAll 取消当前维度全部强制加载 对应原版 removeAll
    private static int RemoveAll(ServerCommandSource source)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("当前维度不支持强制加载");
            return 0;
        }
        //集合在撤票过程中会被重建 先拷一份再遍历
        foreach (var packed in level.GetForceLoadedChunks().ToList())
            level.SetChunkForced(ChunkPos.GetX(packed), ChunkPos.GetZ(packed), false);
        source.SendSuccess("已取消当前维度全部强制加载");
        return 0;
    }

    //ListForceLoad 列出当前维度强制加载区块 对应原版 listForceLoad
    private static int ListForceLoad(ServerCommandSource source)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("当前维度不支持强制加载");
            return 0;
        }
        var chunks = level.GetForceLoadedChunks();
        if (chunks.Count == 0)
        {
            source.SendFailure("当前维度没有强制加载的区块");
            return 0;
        }
        var list = string.Join(", ", chunks.OrderBy(v => v).Select(v => ChunkPos.Unpack(v).ToString()));
        source.SendSuccess($"共 {chunks.Count} 个强制加载区块 {list}");
        return chunks.Count;
    }

    //Query 查询单个区块是否强制加载 对应原版 queryForceLoad
    private static int Query(ServerCommandSource source, (int X, int Z) pos)
    {
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level)
        {
            source.SendFailure("当前维度不支持强制加载");
            return 0;
        }
        var chunk = new ChunkPos(pos.X >> 4, pos.Z >> 4);
        if (level.GetForceLoadedChunks().Contains(chunk.Pack()))
        {
            source.SendSuccess($"区块 {chunk} 处于强制加载状态");
            return 1;
        }
        source.SendFailure($"区块 {chunk} 不在强制加载列表里");
        return 0;
    }
}
