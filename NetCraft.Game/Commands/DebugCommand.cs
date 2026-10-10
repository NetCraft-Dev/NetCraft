using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Game.World.Items.Component;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.Commands;

//DebugCommand debug command, directly reads/writes world block data and drives fake players for troubleshooting
//query prints the block state and block entity nbt; remove sets a block or a whole chunk to air; place places directly by block id
//summon spawns an entity by entity type with optional NBT
//join/leave creates and removes fake players; player chats/executes commands/swings/moves/left and right clicks as a fake player
//tick maps to the whole vanilla /tick tree: ticks per second, freeze, step, sprint
//Results only go to the server console, not chat replies
public static class DebugCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("debug")
            .Requires(s => s.HasPermission(3))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
                        .Executes(QueryBlock))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
                        .Executes(RemoveBlock)))
                .Then(RemoveChunkNode()))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("place")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, BlockInput>.Argument("state", BlockStateArgument.Block())
                        .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
                            .Executes(PlaceBlock)))))
            .Then(SummonNode())
            .Then(JoinNode())
            .Then(LeaveNode())
            .Then(PlayerNode())
            .Then(TickNode())
            .Then(TraceNode())
            .Then(ChunkNode())
            .Then(CommandsNode()));
    }

    //--- debug chunk load state listing ---

    //ChunkNode debug chunk ... lists load state or force loads/unloads chunks
    //list   1 is force-loaded (entities tick) 0 is weakly loaded (load only); the same two tiers the chunk page color blocks use
    //unload <x> <z> / unload all / load <x> <z>
    private static LiteralArgumentBuilder<CommandSourceStack> ChunkNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("chunk")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("mode", IntegerArgumentType.Integer())
                    .Executes(ListChunks)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("unload")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("all")
                    .Executes(UnloadAllChunks))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("x", IntegerArgumentType.Integer())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("z", IntegerArgumentType.Integer())
                        .Executes(UnloadOneChunk))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("load")
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("x", IntegerArgumentType.Integer())
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("z", IntegerArgumentType.Integer())
                        .Executes(LoadOneChunk))));

    //ListChunks lists chunk coordinates in segments by ticket level
    //Printing sixteen per line; thousands on screen crammed into one line is unreadable and floods the console
    private static int ListChunks(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var strong = IntegerArgumentType.GetInteger(context, "mode") != 0;

        var chunkSource = source.Server.Overworld.ChunkSource;
        var list = new List<ChunkPos>();
        foreach (var holder in chunkSource.Holders)
        {
            //Ticket holders above the block-ticking tier are only the outer ring of the load range and do not count as loaded
            if (!ChunkLevel.IsBlockTicking(holder.TicketLevel)) continue;
            //The strong/weak criterion is the simulation level, not the load level
            //Ticket load levels are all 31 within view distance, so FullStatus shows one tier and cannot distinguish that weak-load ring
            if (chunkSource.InEntityTickingRange(holder.Pos.Pack()) != strong) continue;
            list.Add(holder.Pos);
        }
        list.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));

        Log.Info($"[debug] {(strong ? "strong" : "weak")} chunk {list.Count} minecraft:overworld");
        for (var i = 0; i < list.Count; i += 16)
        {
            var line = string.Join(" ", list.Skip(i).Take(16).Select(pos => $"[{pos.X},{pos.Z}]"));
            Log.Info($"[debug] {line}");
        }
        return list.Count;
    }

    //UnloadOneChunk debug chunk unload <x> <z> forcibly unloads the given chunk
    //The ticket is untouched, so if a ticket still covers it, it is pulled back up in a few ticks; this is expected
    private static int UnloadOneChunk(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var pos = new ChunkPos(IntegerArgumentType.GetInteger(context, "x"),
            IntegerArgumentType.GetInteger(context, "z"));
        if (!Forget(source.Server.Overworld.ChunkSource, pos))
        {
            Log.Warning($"[debug] chunk {pos.X} {pos.Z} is not loaded, no need to unload");
            return 0;
        }
        Log.Info($"[debug] unloaded chunk [{pos.X},{pos.Z}]");
        return 1;
    }

    //UnloadAllChunks debug chunk unload all unloads every loaded chunk
    //Take a snapshot first then unload one by one; unloading mutates the holder table and deleting during iteration throws
    private static int UnloadAllChunks(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var chunkSource = source.Server.Overworld.ChunkSource;
        var snapshot = chunkSource.Holders.Select(holder => holder.Pos).ToList();
        var count = 0;
        foreach (var pos in snapshot)
        {
            if (Forget(chunkSource, pos)) count++;
        }
        Log.Info($"[debug] unloaded {count} chunks, holders had {snapshot.Count}");
        return count;
    }

    //LoadOneChunk debug chunk load <x> <z> forcibly pulls up the given chunk
    //Takes the non-blocking path; generation proceeds in the background; this only sends the request
    private static int LoadOneChunk(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var x = IntegerArgumentType.GetInteger(context, "x");
        var z = IntegerArgumentType.GetInteger(context, "z");
        source.Server.Overworld.ChunkSource.GetChunk(x, z, ChunkStatus.FULL, false);
        Log.Info($"[debug] requested chunk load [{x},{z}]");
        return 1;
    }

    //Forget writes to disk first then removes the chunk; force unload does not mean data loss; returns whether a loaded chunk was actually unloaded
    private static bool Forget(ServerChunkCache chunkSource, ChunkPos pos)
    {
        if (chunkSource.GetLoadedChunk(pos.X, pos.Z) is { } chunk)
            chunkSource.ChunkSaveSink?.Invoke(chunk);
        return chunkSource.UnloadChunk(pos);
    }

    //--- debug commands command listing ---

    //CommandsNode debug commands list prints the server's registered command names to the console
    private static LiteralArgumentBuilder<CommandSourceStack> CommandsNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("commands")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
                .Executes(ListCommands));

    //ListCommands takes all command names from the dispatcher root, used to debug "why was a command not dispatched"
    //It lists the registration result; the per-player permission trim on dispatch is not reflected at this layer
    private static int ListCommands(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var names = source.Server.Commands.Dispatcher.GetRoot().GetChildren()
            .Select(child => child.GetName())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Log.Info($"[debug] {names.Count} commands registered: {string.Join(", ", names)}");
        return names.Count;
    }

    //QueryBlock debug query block <pos> prints the block id, registry name, properties and block entity nbt
    private static int QueryBlock(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var pos = GetBlockPos(context, source);

        var state = level.GetBlockState(pos);
        if (state is null)
        {
            Log.Warning($"[debug] query block failed, chunk not loaded pos={FormatPos(pos)}");
            return 0;
        }

        var name = BuiltInRegistries.BLOCK.GetKey(state.Value.Owner);
        var properties = string.Join(",", state.Value.GetValues().Select(v => v.ToString()));
        Log.Info($"[debug] query block pos={FormatPos(pos)} stateId={state.Value.Id} block={name}" +
            (properties.Length == 0 ? "" : $" properties=[{properties}]"));

        var entity = source.Server.BlockEntities.Get(pos);
        if (entity is null)
        {
            Log.Info($"[debug] query block pos={FormatPos(pos)} has no block entity");
            return 1;
        }

        var nbt = new CompoundTag();
        entity.SaveAdditional(nbt);
        Log.Info($"[debug] query block pos={FormatPos(pos)} block entity typeId={entity.TypeId} nbt={NbtUtils.PrettyPrint(nbt, false)}");
        return 1;
    }

    //RemoveBlock debug remove block <pos> sets air and clears the block entity at that position
    private static int RemoveBlock(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var pos = GetBlockPos(context, source);

        if (!ServerBlockUpdates.SetBlock(level, source.Server.PlayerList, pos, Blocks.AIR.DefaultBlockState))
        {
            Log.Warning($"[debug] remove block failed, chunk not loaded or the position is already air pos={FormatPos(pos)}");
            return 0;
        }
        Log.Info($"[debug] removed block pos={FormatPos(pos)}");
        return 1;
    }

    //RemoveChunkNode debug remove chunk [all|<chunk x> <chunk z>] sets all blocks in the chunk to air
    //Without arguments it clears the executor's 16x16x16 sub-chunk; all and given coordinates clear a whole column; only loaded chunks are affected
    private static LiteralArgumentBuilder<CommandSourceStack> RemoveChunkNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("chunk")
            .Executes(context => RemoveChunk(context, null, null, false))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("all")
                .Executes(context => RemoveChunk(context, null, null, true)))
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("x", IntegerArgumentType.Integer())
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("z", IntegerArgumentType.Integer())
                    .Executes(context => RemoveChunk(context,
                        IntegerArgumentType.GetInteger(context, "x"),
                        IntegerArgumentType.GetInteger(context, "z"), true))));

    //RemoveChunk clears the blocks in the target chunk and returns the count cleared
    //Changes are collected through BlockChangeBatch to unify light and block packets; per-cell SetBlock would split clearing a column into tens of thousands of light propagation rounds
    private static int RemoveChunk(CommandContext<CommandSourceStack> context, int? chunkX, int? chunkZ, bool fullColumn)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;

        //By default the executor's chunk is used; the explicit form uses the chunk coordinates directly
        var x = chunkX ?? SectionPos.BlockToSectionCoord(source.PlayerOrThrow.Position.X);
        var z = chunkZ ?? SectionPos.BlockToSectionCoord(source.PlayerOrThrow.Position.Z);
        var chunk = level.ChunkSource.GetLoadedChunk(x, z);
        if (chunk is null)
        {
            source.SendFailure($"chunk {x} {z} is not loaded");
            return 0;
        }

        //The sub-chunk only clears the executor's layer; the whole column clears from the world's lowest section to the highest
        var playerSectionY = SectionPos.BlockToSectionCoord(source.PlayerOrThrow.Position.Y);
        var minSection = fullColumn
            ? level.MinSectionY
            : Math.Clamp(playerSectionY, level.MinSectionY, level.MaxSectionY);
        var maxSection = fullColumn ? level.MaxSectionY : minSection;

        var batch = new BlockChangeBatch(level);
        var air = Blocks.AIR.DefaultBlockState;
        var count = 0;
        var baseX = SectionPos.SectionToBlockCoord(x);
        var baseZ = SectionPos.SectionToBlockCoord(z);
        for (var sectionY = minSection; sectionY <= maxSection; sectionY++)
        {
            var section = chunk.GetSection(sectionY);
            //A section already all air has nothing to clear and is skipped; when clearing a whole column the air sections are the majority
            if (section is null || section.HasOnlyAir()) continue;
            var baseY = SectionPos.SectionToBlockCoord(sectionY);
            for (var offsetY = 0; offsetY < SectionPos.SectionSize; offsetY++)
            for (var offsetZ = 0; offsetZ < SectionPos.SectionSize; offsetZ++)
            for (var offsetX = 0; offsetX < SectionPos.SectionSize; offsetX++)
            {
                if (batch.Apply(new BlockPos(baseX + offsetX, baseY + offsetY, baseZ + offsetZ), air)) count++;
            }
        }

        batch.Flush(source.Server.PlayerList);
        Log.Info($"[debug] cleared chunk x={x} z={z} sections {minSection}..{maxSection} {count} blocks");
        source.SendSuccess($"cleared chunk {x} {z} sections {minSection}..{maxSection} totalling {count} cells");
        return count;
    }

    //PlaceBlock debug place block <block> <pos> directly overwrites the target position with the given block state
    //Does not check replaceability; an existing block at the target is overwritten, aligned with vanilla setblock's overwrite semantics
    private static int PlaceBlock(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var input = context.GetArgument<BlockInput>("state");
        var pos = GetBlockPos(context, source);

        if (!ServerBlockUpdates.SetBlock(level, source.Server.PlayerList, pos, input.State))
        {
            Log.Warning($"[debug] place block failed, chunk not loaded or the state is unchanged pos={FormatPos(pos)}");
            return 0;
        }

        var name = BuiltInRegistries.BLOCK.GetKey(input.State.Owner);
        Log.Info($"[debug] placed block pos={FormatPos(pos)} stateId={input.State.Id} block={name}");

        //The nbt belongs to the block entity at that position; block entity type registration is not wired up here, so no entity can be created for a new block position
        //When an existing entity is hit it is sent as a vanilla block entity data packet; otherwise only a log is written
        if (input.Nbt is null) return 1;
        var entity = source.Server.BlockEntities.Get(pos);
        if (entity is null)
        {
            Log.Warning($"[debug] place block pos={FormatPos(pos)} has no block entity, nbt not written");
            return 1;
        }
        entity.LoadAdditional(input.Nbt);
        source.Server.PlayerList.BroadcastAll(entity.GetUpdatePacket());
        return 1;
    }

    //--- debug summon entity spawning ---

    //SummonNode debug summon <entity type> [{nbt}] [<position>] spawns an entity
    //nbt uses SNBT compound tag syntax, registered at network id 21; the client tokenizes with the vanilla parser so both suggestions and parsing line up
    //When the position is omitted it lands at the executor's position; the type position suggests summonable entities
    private static LiteralArgumentBuilder<CommandSourceStack> SummonNode()
    {
        var entity = RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument(
                "entity", new ResourceArgument(Registries.ENTITY_TYPE.Identifier))
            .Suggests(SuggestEntityTypes)
            .Executes(context => Summon(context, null, null))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
                .Executes(context => Summon(context, null, Vec3Argument.GetCoordinates(context, "pos"))));
        entity.Then(RequiredArgumentBuilder<CommandSourceStack, CompoundTag>.Argument("nbt", CompoundTagArgument.CompoundTag())
            .Executes(context => Summon(context, CompoundTagArgument.GetCompoundTag(context, "nbt"), null))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
                .Executes(context => Summon(context, CompoundTagArgument.GetCompoundTag(context, "nbt"),
                    Vec3Argument.GetCoordinates(context, "pos")))));
        return LiteralArgumentBuilder<CommandSourceStack>.Literal("summon").Then(entity);
    }

    //SuggestEntityTypes suggests entity types, listing only types with a factory, matching what can actually be spawned
    private static Task<Suggestions> SuggestEntityTypes(CommandContext<CommandSourceStack> context,
        SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining.ToLowerInvariant();
        foreach (var holder in BuiltInRegistries.ENTITY_TYPE.ListElements())
        {
            if (holder.Value.Factory is null) continue;
            if (holder.RegisteredName.StartsWith(remaining, StringComparison.Ordinal))
                builder.Add(holder.RegisteredName);
        }
        return builder.BuildFuture();
    }

    //Summon creates an instance by entity type, lands it at the target position and writes the NBT
    private static int Summon(CommandContext<CommandSourceStack> context, CompoundTag? nbt, Coordinates? coordinates)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var id = ResourceArgument.GetResource(context, "entity");
        //The entity type registry is defaulted, so GetOptional must be used or an unknown type silently gets the default
        var type = BuiltInRegistries.ENTITY_TYPE.GetOptional(id);
        if (type is null)
        {
            source.SendFailure($"unknown entity type {id}");
            return 0;
        }
        var entity = type.Create(level);
        if (entity is null)
        {
            source.SendFailure($"entity type {id} has no factory and cannot be created");
            return 0;
        }
        entity.Pos = coordinates?.GetPosition(source) ?? source.PlayerOrThrow.Position;
        //Pos/Motion in the NBT override the position landed above, consistent with vanilla summon placing then reading NBT
        if (nbt is not null) entity.Load(nbt);
        //A drop with no item stack removes itself next tick; debug fills in stone, but a wrong item name in the NBT must error honestly without a silent fallback
        if (entity is ItemEntity drop && drop.Item.IsEmpty())
        {
            if (nbt?.Contains("Item") == true)
            {
                source.SendFailure($"the drop's NBT Item is invalid: item unregistered or count invalid");
                return 0;
            }
            drop.Item = new ItemStack(Items.STONE.BuiltInRegistryHolder, 1, DataComponentPatch.Empty);
        }
        if (!level.AddEntity(entity))
        {
            source.SendFailure($"entity {id} failed to join the world: duplicate Uuid");
            return 0;
        }
        Log.Info($"[debug] summoned entity {id} entityId={entity.EntityId} pos={FormatPos(entity.Pos)}" +
            (nbt is null ? "" : $" nbt={NbtUtils.PrettyPrint(nbt, false)}"));
        source.SendSuccess($"spawned {id} entityId={entity.EntityId}");
        return 1;
    }

    //--- debug join / debug leave fake player lifecycle ---

    //JoinNode debug join <name> creates a fake client going through the real join-world flow
    private static LiteralArgumentBuilder<CommandSourceStack> JoinNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("join")
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("name", StringArgumentType.Word())
                .Executes(JoinPlayer));

    //LeaveNode debug leave <name> removes the fake player and disconnects its fake connection
    private static LiteralArgumentBuilder<CommandSourceStack> LeaveNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("leave")
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("name", StringArgumentType.Word())
                .Executes(LeavePlayer));

    private static int JoinPlayer(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var name = StringArgumentType.GetString(context, "name");
        var fake = source.Server.DebugPlayers.Join(name);
        if (fake is null)
        {
            source.SendFailure($"fake player {name} already exists or the server is full");
            return 0;
        }
        source.SendSuccess($"fake player {name} joined entityId={fake.Player.EntityId}");
        return 1;
    }

    private static int LeavePlayer(CommandContext<CommandSourceStack> context){
        var source = RequireSource(context);
        if (source is null) return 0;
        var name = StringArgumentType.GetString(context, "name");
        if (!source.Server.DebugPlayers.Remove(name))
        {return 0;}
        source.SendSuccess($"fake player {name} removed");
        return 1;}

    //--- debug player fake player actions ---

    //PlayerNode debug player <name> <action> triggers various upstream packets as a fake player
    //chat/command go through the real packet handler; swing waves; move changes position and facing; attack/interact trigger left/right click
    private static LiteralArgumentBuilder<CommandSourceStack> PlayerNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("player")
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("name", StringArgumentType.Word())
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("chat")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("message", StringArgumentType.GreedyString())
                        .Executes(SendChat)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("command")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("command", StringArgumentType.GreedyString())
                        .Executes(RunCommand)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("swing")
                    .Executes(Swing))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("move")
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("to")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("x", DoubleArgumentType.DoubleArg())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("y", DoubleArgumentType.DoubleArg())
                                .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("z", DoubleArgumentType.DoubleArg())
                                    .Executes(MoveTo)))))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("by")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("dx", DoubleArgumentType.DoubleArg())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("dy", DoubleArgumentType.DoubleArg())
                                .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("dz", DoubleArgumentType.DoubleArg())
                                    .Executes(MoveBy)))))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("look")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("yaw", DoubleArgumentType.DoubleArg())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("pitch", DoubleArgumentType.DoubleArg())
                                .Executes(MoveLook))))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("walk")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("direction", StringArgumentType.Word())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("distance", DoubleArgumentType.DoubleArg(0))
                                .Executes(context => Walk(context, 1))
                                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("interval", IntegerArgumentType.Integer(1, 200))
                                    .Executes(context => Walk(context, IntegerArgumentType.GetInteger(context, "interval"))))))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("attack")
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("entity")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                            .Executes(AttackEntity)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("x", IntegerArgumentType.Integer())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("y", IntegerArgumentType.Integer())
                                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("z", IntegerArgumentType.Integer())
                                    .Executes(AttackBlock))))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("interact")
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("entity")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("target", StringArgumentType.Word())
                            .Executes(InteractEntity)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("block")
                        .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("x", IntegerArgumentType.Integer())
                            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("y", IntegerArgumentType.Integer())
                                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("z", IntegerArgumentType.Integer())
                                    .Executes(context => InteractBlock(context, Direction.Up))
                                    .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("face", StringArgumentType.Word())
                                        .Executes(context => InteractBlock(context, ParseFace(context))))))))));

    //SendChat sends a chat packet as the fake player through the real HandleChat broadcast
    private static int SendChat(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var message = StringArgumentType.GetString(context, "message");
        fake.Listener.HandleChat(new ServerboundChatPacket(message, 0, 0, null, 0, 0, 0));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} sent chat");
        return 1;
    }

    //RunCommand runs a command as the fake player, with its permission from its level in ops.json
    private static int RunCommand(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var command = StringArgumentType.GetString(context, "command");
        fake.Listener.HandleChatCommand(new ServerboundChatCommandPacket(command));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} ran command {command}");
        return 1;
    }

    //Swing waves, broadcast to other players through HandleAnimate
    private static int Swing(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.Listener.HandleAnimate(new ServerboundSwingPacket(InteractionHand.MainHand));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} swung");
        return 1;
    }

    private static int MoveTo(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.StopWalk();
        fake.Player.Position = ReadPosition(context, "x", "y", "z");
        source.SendSuccess($"fake player {fake.Player.Profile.Name} positioned to {FormatPos(fake.Player.Position)}");
        return 1;
    }

    private static int MoveBy(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.StopWalk();
        var offset = ReadPosition(context, "dx", "dy", "dz");
        var current = fake.Player.Position;
        fake.Player.Position = new Vec3(current.X + offset.X, current.Y + offset.Y, current.Z + offset.Z);
        source.SendSuccess($"fake player {fake.Player.Profile.Name} moved to {FormatPos(fake.Player.Position)}");
        return 1;
    }

    private static int MoveLook(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.Player.Yaw = (float)DoubleArgumentType.GetDouble(context, "yaw");
        fake.Player.Pitch = (float)DoubleArgumentType.GetDouble(context, "pitch");
        source.SendSuccess($"fake player {fake.Player.Profile.Name} facing yaw={fake.Player.Yaw} pitch={fake.Player.Pitch}");
        return 1;
    }

    //Walk steps toward the given direction, advanced a quarter block per tick by the server; position changes are synced out by the entity tracker
    private static int Walk(CommandContext<CommandSourceStack> context, int interval)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var direction = StringArgumentType.GetString(context, "direction").ToLowerInvariant();
        var offset = DirectionOffset(direction);
        if (offset is null)
        {
            source.SendFailure($"unknown direction {direction}; available north/south/east/west/up/down");
            return 0;
        }
        var distance = DoubleArgumentType.GetDouble(context, "distance");
        if (distance <= 0)
        {
            source.SendFailure("the walk distance must be greater than 0");
            return 0;
        }
        var current = fake.Player.Position;
        var target = new Vec3(
            current.X + offset.Value.X * distance,
            current.Y + offset.Value.Y * distance,
            current.Z + offset.Value.Z * distance);
        fake.StartWalk(target, interval);
        source.SendSuccess($"fake player {fake.Player.Profile.Name} starts walking {direction} {distance} blocks, one step every {interval} ticks");
        return 1;
    }

    //AttackEntity triggers a left-click attack; the target supports an online player name or an entity id
    private static int AttackEntity(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var target = StringArgumentType.GetString(context, "target");
        var entityId = ResolveEntityId(source, target);
        if (entityId is null)
        {
            source.SendFailure($"target {target} not found; only online player names or entity ids are supported");
            return 0;
        }
        fake.Listener.HandleAttack(new ServerboundAttackPacket(entityId.Value));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} attacked entity id={entityId}");
        return 1;
    }

    //AttackBlock triggers a left-click block break; creative breaks instantly, survival only starts mining
    private static int AttackBlock(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var pos = ReadBlockPos(context, "x", "y", "z");
        fake.Listener.HandlePlayerAction(new ServerboundPlayerActionPacket(
            ServerboundPlayerActionPacket.ActionType.StartDestroyBlock, pos, Direction.Up, 0));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} started mining {FormatPos(pos)}");
        return 1;
    }

    //InteractEntity triggers a right-click on an entity; this project has no interact behavior, so it plays the swing animation like vanilla
    private static int InteractEntity(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var target = StringArgumentType.GetString(context, "target");
        var entityId = ResolveEntityId(source, target);
        if (entityId is null)
        {
            source.SendFailure($"target {target} not found; only online player names or entity ids are supported");
            return 0;
        }
        fake.Listener.HandleInteract(new ServerboundInteractPacket(
            entityId.Value, InteractionHand.MainHand, Vec3.Zero, false));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} interacted with entity id={entityId}");
        return 1;
    }

    //InteractBlock triggers a right-click on a block, going through the block behavior and held-block placement chain
    private static int InteractBlock(CommandContext<CommandSourceStack> context, Direction face)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var pos = ReadBlockPos(context, "x", "y", "z");
        var hit = new BlockHitResult(pos, face,
            new Vec3(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), false);
        fake.Listener.HandleUseItemOn(new ServerboundUseItemOnPacket(InteractionHand.MainHand, hit, 0));
        source.SendSuccess($"fake player {fake.Player.Profile.Name} right-clicked block {FormatPos(pos)} face={face}");
        return 1;
    }

    //ParseFace parses the hit face argument; defaults to up
    private static Direction ParseFace(CommandContext<CommandSourceStack> context)
    {
        var name = StringArgumentType.GetString(context, "face").ToLowerInvariant();
        return name switch
        {
            "north" => Direction.North,
            "south" => Direction.South,
            "east" => Direction.East,
            "west" => Direction.West,
            "down" => Direction.Down,
            _ => Direction.Up,
        };
    }

    //--- debug tick tick rate ---

    //MaxTickRate max ticks per second, maps to vanilla TickCommand.MAX_TICKRATE
    private const float MaxTickRate = 10000f;

    //TickNode debug tick <subcommand> the whole vanilla /tick tree moved here
    //query checks status; rate changes ticks per second; freeze/unfreeze; step advances ticks while frozen; sprint sprints
    private static LiteralArgumentBuilder<CommandSourceStack> TickNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("tick")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Executes(TickQuery))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("rate")
                .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("rate",
                        FloatArgumentType.FloatArg(ServerTickRateManager.MinTickRate, MaxTickRate))
                    .Suggests((_, builder) => SuggestStrings(builder, "20"))
                    .Executes(SetTickRate)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("step")
                .Executes(context => Step(context, 1))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
                    .Executes(StopStepping))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(1))
                    .Suggests((_, builder) => SuggestStrings(builder, "1t", "1s"))
                    .Executes(context => Step(context, context.GetArgument<int>("time")))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("sprint")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
                    .Executes(StopSprinting))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(1))
                    .Suggests((_, builder) => SuggestStrings(builder, "60s", "1d", "3d"))
                    .Executes(Sprint)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("unfreeze")
                .Executes(context => SetFreeze(context, false)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("freeze")
                .Executes(context => SetFreeze(context, true)));

    //TickQuery debug tick query prints status, average per-tick time, per-tick target and percentiles, maps to vanilla tickQuery
    private static int TickQuery(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var manager = source.Server.TickRate;
        var average = source.Server.AverageTickTimeNanos;
        var rateText = manager.TickRate.ToString("F1");

        if (manager.IsSprinting)
        {
            source.SendSuccess("tick status: sprinting");
            source.SendSuccess($"tick rate {rateText} per-tick time {NanosToMillis(average)} ms");
        }
        else
        {
            //The lagging test uses average per-tick time exceeding the per-tick target, maps to vanilla nanosecondsPerTick < averageTickTimeNanos
            var status = manager.IsFrozen ? "frozen"
                : manager.NanosecondsPerTick < average ? "lagging"
                : "ok";
            source.SendSuccess($"tick status {status}");
            source.SendSuccess($"tick rate {rateText} per-tick time {NanosToMillis(average)}ms" +
                $"per-tick target {manager.MillisecondsPerTick:F1}ms");
        }

        var samples = source.Server.TickTimesNanos.ToArray();
        if (samples.Length == 0) return (int)manager.TickRate;
        Array.Sort(samples);
        source.SendSuccess($"per-tick percentiles p50 {NanosToMillis(samples[samples.Length / 2])} ms " +
            $"p95 {NanosToMillis(samples[(int)(samples.Length * 0.95)])} ms " +
            $"p99 {NanosToMillis(samples[(int)(samples.Length * 0.99)])} ms samples {samples.Length}");
        return (int)manager.TickRate;
    }

    //SetTickRate debug tick rate <ticks per second>, maps to vanilla setTickingRate
    private static int SetTickRate(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var rate = FloatArgumentType.GetFloat(context, "rate");
        source.Server.TickRate.SetTickRate(rate);
        source.SendSuccess($"ticks per second set to {rate:F1}");
        return (int)rate;
    }

    //Step debug tick step [<time>] can only step while frozen, maps to vanilla step
    private static int Step(CommandContext<CommandSourceStack> context, int advance)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (!source.Server.TickRate.StepGameIfPaused(advance))
        {
            source.SendFailure("the world is not frozen; run debug tick freeze first");
            return 0;
        }
        source.SendSuccess($"stepped {advance} ticks");
        return 1;
    }

    //StopStepping debug tick step stop, maps to vanilla stopStepping
    private static int StopStepping(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (!source.Server.TickRate.StopStepping())
        {
            source.SendFailure("there is no pending step");
            return 0;
        }
        source.SendSuccess("stepping stopped");
        return 1;
    }

    //Sprint debug tick sprint <time> sprints the given number of ticks, maps to vanilla sprint
    private static int Sprint(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var ticks = context.GetArgument<int>("time");
        var interrupted = source.Server.TickRate.RequestGameToSprint(ticks);
        if (interrupted) source.SendSuccess("the previous sprint was interrupted");
        source.SendSuccess($"sprinting {ticks} ticks");
        return 1;
    }

    //StopSprinting debug tick sprint stop, maps to vanilla stopSprinting
    private static int StopSprinting(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (!source.Server.TickRate.StopSprinting())
        {
            source.SendFailure("not sprinting");
            return 0;
        }
        source.SendSuccess("sprint stopped");
        return 1;
    }

    //SetFreeze debug tick freeze|unfreeze stops sprint and step before freezing, maps to vanilla setFreeze
    private static int SetFreeze(CommandContext<CommandSourceStack> context, bool freeze)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var manager = source.Server.TickRate;
        if (freeze)
        {
            if (manager.IsSprinting) manager.StopSprinting();
            if (manager.IsSteppingForward) manager.StopStepping();
        }
        manager.SetFrozen(freeze);
        source.SendSuccess(freeze ? "the world is frozen" : "the world is unfrozen");
        return freeze ? 1 : 0;
    }

    //NanosToMillis nanoseconds to milliseconds with one decimal, maps to vanilla nanosToMilisString
    private static string NanosToMillis(long nanos) => (nanos / 1_000_000.0).ToString("F1");

    //SuggestStrings suggests prefix-matching entries from fixed candidates
    private static Task<Suggestions> SuggestStrings(SuggestionsBuilder builder, params string[] candidates)
    {
        var remaining = builder.Remaining;
        foreach (var candidate in candidates)
            if (candidate.StartsWith(remaining, StringComparison.Ordinal))
                builder.Add(candidate);
        return builder.BuildFuture();
    }

    //--- debug trace runtime capture ---

    //TraceNode debug trace on|off records this process's runtime events to a file
    //on begins a capture and prints the file being written; off ends it and prints where the file landed
    //The capture carries CPU samples and the runtime events, a nettrace readable by PerfView/dotnet-trace/Visual Studio
    private static LiteralArgumentBuilder<CommandSourceStack> TraceNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("trace")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("on")
                .Executes(StartTrace))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("off")
                .Executes(StopTrace));

    //StartTrace debug trace on begins a capture
    //A capture started by the --trace startup flag reaches here too, and this reports it as already running
    private static int StartTrace(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var path = RuntimeTraceRecorder.Start(AppPaths.TracesDir);
        if (path is null)
        {
            source.SendFailure("a trace is already running or the capture could not be started; see the server log");
            return 0;
        }
        source.SendSuccess($"trace recording to {path}; run debug trace off to finish it");
        return 1;
    }

    //StopTrace debug trace off ends the capture and reports the file that was written
    private static int StopTrace(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var path = RuntimeTraceRecorder.Stop();
        if (path is null)
        {
            source.SendFailure("no trace is running");
            return 0;
        }
        source.SendSuccess($"trace written to {path}");
        return 1;
    }

    //--- common helpers ---

    //RequireFakePlayer gets the command source and the target fake player, already reporting failure when missing
    private static (ServerCommandSource? Source, DebugPlayer? Fake) RequireFakePlayer(
        CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return (null, null);
        var name = StringArgumentType.GetString(context, "name");
        var fake = source.Server.DebugPlayers.Find(name);
        if (fake is null)
        {
            source.SendFailure($"fake player {name} does not exist; create it first with debug join {name}");
            return (source, null);
        }
        return (source, fake);
    }

    //ResolveEntityId resolves the target entity id, first by online player name then by integer entity id
    private static int? ResolveEntityId(ServerCommandSource source, string target)
    {
        var online = source.Server.PlayerList.GetPlayerByName(target);
        if (online is not null) return online.EntityId;
        return int.TryParse(target, out var id) ? id : null;
    }

    //ReadPosition reads three double arguments into a coordinate
    private static Vec3 ReadPosition(CommandContext<CommandSourceStack> context, string x, string y, string z)
        => new(DoubleArgumentType.GetDouble(context, x),
            DoubleArgumentType.GetDouble(context, y),
            DoubleArgumentType.GetDouble(context, z));

    //ReadBlockPos reads three integer arguments into a block coordinate
    private static BlockPos ReadBlockPos(CommandContext<CommandSourceStack> context, string x, string y, string z)
        => new(IntegerArgumentType.GetInteger(context, x),
            IntegerArgumentType.GetInteger(context, y),
            IntegerArgumentType.GetInteger(context, z));

    //DirectionOffset converts a direction word to a unit offset; north is -Z and south is +Z, consistent with the game
    private static (double X, double Y, double Z)? DirectionOffset(string direction) => direction switch
    {
        "north" => (0, 0, -1),
        "south" => (0, 0, 1),
        "east" => (1, 0, 0),
        "west" => (-1, 0, 0),
        "up" => (0, 1, 0),
        "down" => (0, -1, 0),
        _ => null,
    };

    private static ServerCommandSource? RequireSource(CommandContext<CommandSourceStack> context)
        => context.GetSource() as ServerCommandSource;

    //GetBlockPos resolves the coordinate argument from the executor's position, floored to a block cell
    private static BlockPos GetBlockPos(CommandContext<CommandSourceStack> context, ServerCommandSource source)
    {
        var position = Vec3Argument.GetCoordinates(context, "pos").GetPosition(source);
        return new BlockPos((int)Math.Floor(position.X), (int)Math.Floor(position.Y), (int)Math.Floor(position.Z));
    }

    private static string FormatPos(BlockPos pos) => $"{pos.X} {pos.Y} {pos.Z}";

    private static string FormatPos(Vec3 pos) => $"{pos.X:0.##} {pos.Y:0.##} {pos.Z:0.##}";
}
