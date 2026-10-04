using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//FillBiomeCommand fillbiome 命令对应原版 net.minecraft.server.commands.FillBiomeCommand
//fillbiome <起点> <终点> <生物群系> [replace <过滤群系>]
//区域按 4 格量化后整片改写已加载区块的群系 数据改完重发给视野内的玩家
public static class FillBiomeCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("fillbiome")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("from", BlockPosArgument.BlockPos())
                .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("to", BlockPosArgument.BlockPos())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("biome",
                            new ResourceArgument(Registries.BIOME.Identifier))
                        .Executes(context => Fill(context, _ => true))
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("replace")
                            .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("filter",
                                    new ResourceArgument(Registries.BIOME.Identifier))
                                .Executes(context =>
                                {
                                    var filter = ResourceArgument.GetBiome(context, "filter");
                                    return Fill(context, current => ReferenceEquals(current.Value, filter.Value));
                                })))))));
    }

    //Quantize 方块坐标向下取到 4 的倍数 对应原版 QuartPos 往返量化
    private static BlockPos Quantize(BlockPos pos) => new(pos.X >> 2 << 2, pos.Y >> 2 << 2, pos.Z >> 2 << 2);

    //Fill 区域整体改写 有未加载区块时按原版整体失败不改任何数据
    private static int Fill(CommandContext<CommandSourceStack> context, Func<Holder<Biome>, bool> filter)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;

        var from = Quantize(BlockPosArgument.GetBlockPos(context, "from"));
        var to = Quantize(BlockPosArgument.GetBlockPos(context, "to"));
        var biomeId = ResourceArgument.GetResource(context, "biome");
        var biome = ResourceArgument.GetBiome(context, "biome");

        var minX = Math.Min(from.X, to.X);
        var minY = Math.Min(from.Y, to.Y);
        var minZ = Math.Min(from.Z, to.Z);
        var maxX = Math.Max(from.X, to.X);
        var maxY = Math.Max(from.Y, to.Y);
        var maxZ = Math.Max(from.Z, to.Z);

        var volume = (long)(maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1);
        var limit = source.Server.GameRules.GetInt(GameRules.MaxBlockModifications);
        if (volume > limit)
        {
            source.SendFailure($"区域过大 上限 {limit} 格 当前 {volume} 格");
            return 0;
        }

        //先把区域内区块取齐 未加载的按原版整单失败 避免写入一半
        var chunks = new List<ChunkAccess>();
        for (var chunkZ = minZ >> 4; chunkZ <= maxZ >> 4; chunkZ++)
        for (var chunkX = minX >> 4; chunkX <= maxX >> 4; chunkX++)
        {
            if (level.GetChunk(new ChunkPos(chunkX, chunkZ)) is not { } chunk)
            {
                source.SendFailure("区域内存在未加载的区块");
                return 0;
            }
            chunks.Add(chunk);
        }

        var changed = 0;
        foreach (var chunk in chunks)
            changed += FillChunk(chunk, minX, minY, minZ, maxX, maxY, maxZ, biome, filter);

        ResendBiomes(source.Server, chunks);
        source.SendSuccess($"已将 {changed} 个生物群系单元替换为 {biomeId}");
        return changed;
    }

    //FillChunk 逐区段逐 quart 单元改写 与原版 BiomeResolver 判定口径一致
    //区域判的是方块坐标 即 quart 坐标乘 4
    private static int FillChunk(ChunkAccess chunk, int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
        Holder<Biome> biome, Func<Holder<Biome>, bool> filter)
    {
        var changed = 0;
        var baseX = chunk.Pos.X * 16;
        var baseZ = chunk.Pos.Z * 16;
        for (var i = 0; i < chunk.SectionsCount; i++)
        {
            var sectionY = chunk.MinSectionY + i;
            if (chunk.GetSection(sectionY) is not { } section) continue;
            var baseY = sectionY * 16;
            for (var y = 0; y < 4; y++)
            {
                var blockY = baseY + (y << 2);
                if (blockY < minY || blockY > maxY) continue;
                for (var z = 0; z < 4; z++)
                {
                    var blockZ = baseZ + (z << 2);
                    if (blockZ < minZ || blockZ > maxZ) continue;
                    for (var x = 0; x < 4; x++)
                    {
                        var blockX = baseX + (x << 2);
                        if (blockX < minX || blockX > maxX) continue;
                        if (!filter(section.GetNoiseBiome(x, y, z))) continue;
                        section.SetBiome(x, y, z, biome);
                        changed++;
                    }
                }
            }
        }
        return changed;
    }

    //ResendBiomes 把改动过的区块重发给视野内的玩家 客户端据此刷新群系配色
    //原版按区块订阅玩家下发 nc 用视距判断等价范围
    private static void ResendBiomes(MinecraftServer server, IReadOnlyList<ChunkAccess> chunks)
    {
        foreach (var player in server.PlayerList.Players)
        {
            var centerX = (int)Math.Floor(player.Position.X) >> 4;
            var centerZ = (int)Math.Floor(player.Position.Z) >> 4;
            var visible = new List<ChunkAccess>();
            foreach (var chunk in chunks)
                if (Math.Abs(chunk.Pos.X - centerX) <= player.ViewDistanceChunks
                    && Math.Abs(chunk.Pos.Z - centerZ) <= player.ViewDistanceChunks)
                    visible.Add(chunk);
            if (visible.Count > 0) player.Connection.Send(ClientboundChunksBiomesPacket.ForChunks(visible));
        }
    }
}
