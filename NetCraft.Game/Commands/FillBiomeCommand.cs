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

//FillBiomeCommand fillbiome command, maps to vanilla net.minecraft.server.commands.FillBiomeCommand
//fillbiome <begin> <end> <biome> [replace <filter biome>]
//The region is quantized to 4 and rewrites the biome of loaded chunks wholesale; after the data change it is re-sent to players in view
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

    //Quantize floors a block coordinate to a multiple of 4, maps to vanilla QuartPos round-trip quantization
    private static BlockPos Quantize(BlockPos pos) => new(pos.X >> 2 << 2, pos.Y >> 2 << 2, pos.Z >> 2 << 2);

    //Fill rewrites the whole region; when there are unloaded chunks it fails as a whole like vanilla and changes nothing
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
            source.SendFailure($"region too large, limit {limit} cells, current {volume} cells");
            return 0;
        }

        //Take all chunks in the region first; unloaded ones fail the whole request like vanilla, to avoid a half write
        var chunks = new List<ChunkAccess>();
        for (var chunkZ = minZ >> 4; chunkZ <= maxZ >> 4; chunkZ++)
        for (var chunkX = minX >> 4; chunkX <= maxX >> 4; chunkX++)
        {
            if (level.GetChunk(new ChunkPos(chunkX, chunkZ)) is not { } chunk)
            {
                source.SendFailure("there are unloaded chunks in the region");
                return 0;
            }
            chunks.Add(chunk);
        }

        var changed = 0;
        foreach (var chunk in chunks)
            changed += FillChunk(chunk, minX, minY, minZ, maxX, maxY, maxZ, biome, filter);

        ResendBiomes(source.Server, chunks);
        source.SendSuccess($"replaced {changed} biome cells with {biomeId}");
        return changed;
    }

    //FillChunk rewrites section by section and quart unit by quart unit, consistent with vanilla BiomeResolver
    //The region is judged by block coordinates, i.e. quart coordinates times 4
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

    //ResendBiomes re-sends the changed chunks to players in view; the client refreshes biome colors from it
    //Vanilla sends to players subscribed to the chunk; nc uses view distance for the equivalent range
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
