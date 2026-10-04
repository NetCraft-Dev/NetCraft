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
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Commands;

//DebugCommand debug 命令 直接读写世界方块数据与驱动假玩家用于排查问题
//query 打印方块状态与方块实体nbt remove 把方块或整个区块置成空气 place 按方块 id 直接放置
//summon 按实体类型与可选 NBT 生成实体
//join/leave 造与移除假玩家 player 以假玩家身份聊天/执行命令/挥手/移动/左右键
//tick 对应原版 /tick 整棵树 管每秒刻数 冻结 步进 加速跑
//结果只进服务端控制台 不走聊天回执
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
            .Then(ChunkNode())
            .Then(CommandsNode()));
    }

    //--- debug chunk 加载状态清单 ---

    //ChunkNode debug chunk ... 列出加载状态或强制装卸区块
    //list   1 是强加载(实体可 tick) 0 是弱加载(仅加载) 判定与区块页那两档色块用的是同一套
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

    //ListChunks 按票等级分段列出区块坐标
    //逐行打十六个 满屏几千个挤成一行既看不清也刷不动控制台
    private static int ListChunks(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var strong = IntegerArgumentType.GetInteger(context, "mode") != 0;

        var chunkSource = source.Server.Overworld.ChunkSource;
        var list = new List<ChunkPos>();
        foreach (var holder in chunkSource.Holders)
        {
            //等级越过方块可 tick 档的持有器只是加载范围的外圈 不算加载
            if (!ChunkLevel.IsBlockTicking(holder.TicketLevel)) continue;
            //强弱的判据是模拟等级而不是加载等级
            //加载票在视距内一律 31 用 FullStatus 看全是同一档 分不出那一圈弱加载
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

    //UnloadOneChunk debug chunk unload <x> <z> 强制卸载指定区块
    //票不动 所以还有票覆盖时下几 tick 会被重新拉起来 这是预期行为
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

    //UnloadAllChunks debug chunk unload all 卸载全部已加载区块
    //先取快照再逐个卸 卸载会动持有器表 边遍历边删会抛
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

    //LoadOneChunk debug chunk load <x> <z> 强制拉起指定区块
    //走非阻塞路径 生成在后台推进 这里只负责把请求发出去
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

    //Forget 先落盘再摘掉区块 强制卸不等于丢数据 返回是否真的卸掉了一个已加载的区块
    private static bool Forget(ServerChunkCache chunkSource, ChunkPos pos)
    {
        if (chunkSource.GetLoadedChunk(pos.X, pos.Z) is { } chunk)
            chunkSource.ChunkSaveSink?.Invoke(chunk);
        return chunkSource.UnloadChunk(pos);
    }

    //--- debug commands 命令清单 ---

    //CommandsNode debug commands list 把服务端已注册的命令名打到控制台
    private static LiteralArgumentBuilder<CommandSourceStack> CommandsNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("commands")
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("list")
                .Executes(ListCommands));

    //ListCommands 从命令分发器根节点取全部命令名 排查"某条命令为什么没下发"时用
    //列的是注册结果 不下发到某个玩家的权限裁剪不在这一层体现
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

    //QueryBlock debug query block <pos> 打印方块id 注册名 属性与方块实体nbt
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

    //RemoveBlock debug remove block <pos> 置空气并清理该位置方块实体
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

    //RemoveChunkNode debug remove chunk [all|<区块x> <区块z>] 把区块内的方块整体置成空气
    //不带参数清执行者所在的 16x16x16 子区块 all 与指定坐标清整列 只操作已加载的区块
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

    //RemoveChunk 清空目标区块内的方块 返回被清掉的格数
    //变更收集走 BlockChangeBatch 统一发光照与方块包 逐格 SetBlock 会把整列清空拆成几万轮光照传播
    private static int RemoveChunk(CommandContext<CommandSourceStack> context, int? chunkX, int? chunkZ, bool fullColumn)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;

        //缺省用执行者所在区块 指定形式直接按区块坐标
        var x = chunkX ?? SectionPos.BlockToSectionCoord(source.PlayerOrThrow.Position.X);
        var z = chunkZ ?? SectionPos.BlockToSectionCoord(source.PlayerOrThrow.Position.Z);
        var chunk = level.ChunkSource.GetLoadedChunk(x, z);
        if (chunk is null)
        {
            source.SendFailure($"区块 {x} {z} 未加载");
            return 0;
        }

        //子区块只清执行者所在那一层 整列从世界最低段清到最高段
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
            //整段已是空气就没有可清的直接跳过 整列清空时空中段占多数
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
        source.SendSuccess($"已清空区块 {x} {z} 段 {minSection}..{maxSection} 共 {count} 格");
        return count;
    }

    //PlaceBlock debug place block <方块> <pos> 按给定方块状态直接覆盖目标位置
    //不校验可替换性 目标位置已有方块会被顶掉 对齐原版 setblock 的覆盖语义
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

        //nbt 归属该位置的方块实体 本作方块实体类型注册未接入 无法为新区块位置建实体
        //命中已有实体时按原版方块实体数据包下发 未命中只记日志
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

    //--- debug summon 实体生成 ---

    //SummonNode debug summon <实体类型> [{nbt}] [<坐标>] 生成一个实体
    //nbt 走 SNBT 复合标签语法 注册在网络 id 21 客户端按原版解析器切词 补全与解析都能对上
    //坐标省略时落在执行者位置 类型位置给可召唤实体的补全
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

    //SuggestEntityTypes 补全实体类型 只列有工厂的类型 与实际能生成的范围一致
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

    //Summon 按实体类型创建实例落到目标位置并写入 NBT
    private static int Summon(CommandContext<CommandSourceStack> context, CompoundTag? nbt, Coordinates? coordinates)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (source.PlayerOrThrow.Level is not PersistentServerLevel level) return 0;
        var id = ResourceArgument.GetResource(context, "entity");
        //实体类型注册表带默认值 必须走 GetOptional 否则未知类型会静默拿到默认项
        var type = BuiltInRegistries.ENTITY_TYPE.GetOptional(id);
        if (type is null)
        {
            source.SendFailure($"未知实体类型 {id}");
            return 0;
        }
        var entity = type.Create(level);
        if (entity is null)
        {
            source.SendFailure($"实体类型 {id} 没有工厂无法创建");
            return 0;
        }
        entity.Pos = coordinates?.GetPosition(source) ?? source.PlayerOrThrow.Position;
        //NBT 里的 Pos/Motion 覆盖上面落的坐标 与原版 summon 先定位再读 NBT 的次序一致
        if (nbt is not null) entity.Load(nbt);
        //掉落物没有物品栈下一 tick 就自移除 调试时补石头 但 NBT 写错物品名要如实报错不静默兜底
        if (entity is ItemEntity drop && drop.Item.IsEmpty())
        {
            if (nbt?.Contains("Item") == true)
            {
                source.SendFailure($"掉落物 NBT 的 Item 无效 物品未注册或 count 不合法");
                return 0;
            }
            drop.Item = new ItemStack(Items.STONE.BuiltInRegistryHolder, 1, DataComponentPatch.Empty);
        }
        if (!level.AddEntity(entity))
        {
            source.SendFailure($"实体 {id} 加入世界失败 Uuid 重复");
            return 0;
        }
        Log.Info($"[debug] summoned entity {id} entityId={entity.EntityId} pos={FormatPos(entity.Pos)}" +
            (nbt is null ? "" : $" nbt={NbtUtils.PrettyPrint(nbt, false)}"));
        source.SendSuccess($"已生成 {id} entityId={entity.EntityId}");
        return 1;
    }

    //--- debug join / debug leave 假玩家生命周期 ---

    //JoinNode debug join <名字> 造一个假客户端走真实加入世界流程
    private static LiteralArgumentBuilder<CommandSourceStack> JoinNode()
        => LiteralArgumentBuilder<CommandSourceStack>.Literal("join")
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("name", StringArgumentType.Word())
                .Executes(JoinPlayer));

    //LeaveNode debug leave <名字> 移出假玩家并断开它的假连接
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
            source.SendFailure($"假玩家 {name} 已存在或服务端已满员");
            return 0;
        }
        source.SendSuccess($"假玩家{name}已加入entityId={fake.Player.EntityId}");
        return 1;
    }

    private static int LeavePlayer(CommandContext<CommandSourceStack> context){
        var source = RequireSource(context);
        if (source is null) return 0;
        var name = StringArgumentType.GetString(context, "name");
        if (!source.Server.DebugPlayers.Remove(name))
        {return 0;}
        source.SendSuccess($"假玩家 {name} 已移出");
        return 1;}

    //--- debug player 假玩家操作 ---

    //PlayerNode debug player <名字> <操作> 以假玩家身份触发各类上行包
    //chat/command 走真实包处理器 swing 挥手 move 改位置朝向 attack/interact 触发左右键
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

    //SendChat 以假玩家身份发聊天包 走真实 HandleChat 广播
    private static int SendChat(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var message = StringArgumentType.GetString(context, "message");
        fake.Listener.HandleChat(new ServerboundChatPacket(message, 0, 0, null, 0, 0, 0));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}已发送聊天");
        return 1;
    }

    //RunCommand 以假玩家身份执行命令 权限按它在 ops.json 里的等级
    private static int RunCommand(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var command = StringArgumentType.GetString(context, "command");
        fake.Listener.HandleChatCommand(new ServerboundChatCommandPacket(command));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}已执行命令 {command}");
        return 1;
    }

    //Swing 挥手 走 HandleAnimate 广播给其他玩家
    private static int Swing(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.Listener.HandleAnimate(new ServerboundSwingPacket(InteractionHand.MainHand));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}已挥手");
        return 1;
    }

    private static int MoveTo(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.StopWalk();
        fake.Player.Position = ReadPosition(context, "x", "y", "z");
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}已定位到{FormatPos(fake.Player.Position)}");
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
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}已位移到 {FormatPos(fake.Player.Position)}");
        return 1;
    }

    private static int MoveLook(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        fake.Player.Yaw = (float)DoubleArgumentType.GetDouble(context, "yaw");
        fake.Player.Pitch = (float)DoubleArgumentType.GetDouble(context, "pitch");
        source.SendSuccess($"假玩家 {fake.Player.Profile.Name} 朝向 yaw={fake.Player.Yaw} pitch={fake.Player.Pitch}");
        return 1;
    }

    //Walk 朝指定方向逐步行走 由服务端每刻推进一格走四分之一格 位置变化会被实体追踪同步出去
    private static int Walk(CommandContext<CommandSourceStack> context, int interval)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var direction = StringArgumentType.GetString(context, "direction").ToLowerInvariant();
        var offset = DirectionOffset(direction);
        if (offset is null)
        {
            source.SendFailure($"未知方向 {direction} 可用 north/south/east/west/up/down");
            return 0;
        }
        var distance = DoubleArgumentType.GetDouble(context, "distance");
        if (distance <= 0)
        {
            source.SendFailure("行走距离必须大于0");
            return 0;
        }
        var current = fake.Player.Position;
        var target = new Vec3(
            current.X + offset.Value.X * distance,
            current.Y + offset.Value.Y * distance,
            current.Z + offset.Value.Z * distance);
        fake.StartWalk(target, interval);
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}开始向{direction}走{distance}格每步{interval}刻");
        return 1;
    }

    //AttackEntity 触发左键攻击 目标支持在线玩家名或实体 id
    private static int AttackEntity(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var target = StringArgumentType.GetString(context, "target");
        var entityId = ResolveEntityId(source, target);
        if (entityId is null)
        {
            source.SendFailure($"找不到目标{target}只支持在线玩家名或实体 id");
            return 0;
        }
        fake.Listener.HandleAttack(new ServerboundAttackPacket(entityId.Value));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}攻击实体id={entityId}");
        return 1;
    }

    //AttackBlock 触发左键挖方块 创造模式即时破坏 生存模式只开始挖掘
    private static int AttackBlock(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var pos = ReadBlockPos(context, "x", "y", "z");
        fake.Listener.HandlePlayerAction(new ServerboundPlayerActionPacket(
            ServerboundPlayerActionPacket.ActionType.StartDestroyBlock, pos, Direction.Up, 0));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}开始挖{FormatPos(pos)}");
        return 1;
    }

    //InteractEntity 触发右键实体 本作没有交互行为 按原版先播挥手动画
    private static int InteractEntity(CommandContext<CommandSourceStack> context)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var target = StringArgumentType.GetString(context, "target");
        var entityId = ResolveEntityId(source, target);
        if (entityId is null)
        {
            source.SendFailure($"找不到目标{target}只支持在线玩家名或实体id");
            return 0;
        }
        fake.Listener.HandleInteract(new ServerboundInteractPacket(
            entityId.Value, InteractionHand.MainHand, Vec3.Zero, false));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}交互实体id={entityId}");
        return 1;
    }

    //InteractBlock 触发右键方块 走方块行为与手持方块放置链路
    private static int InteractBlock(CommandContext<CommandSourceStack> context, Direction face)
    {
        var (source, fake) = RequireFakePlayer(context);
        if (source is null || fake is null) return 0;
        var pos = ReadBlockPos(context, "x", "y", "z");
        var hit = new BlockHitResult(pos, face,
            new Vec3(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), false);
        fake.Listener.HandleUseItemOn(new ServerboundUseItemOnPacket(InteractionHand.MainHand, hit, 0));
        source.SendSuccess($"假玩家{fake.Player.Profile.Name}右键方块 {FormatPos(pos)} 面={face}");
        return 1;
    }

    //ParseFace 解析命中面参数 缺省回上方
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

    //--- debug tick 刻速率 ---

    //MaxTickRate 每秒刻数上限 对应原版 TickCommand.MAX_TICKRATE
    private const float MaxTickRate = 10000f;

    //TickNode debug tick <子命令> 原版 /tick 整棵树搬到这里
    //query 查状态 rate 改每秒刻数 freeze/unfreeze 冻结解冻 step 冻结下走刻 sprint 加速跑
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

    //TickQuery debug tick query 打印状态 平均单拍耗时 单刻目标与分位数 对应原版 tickQuery
    private static int TickQuery(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var manager = source.Server.TickRate;
        var average = source.Server.AverageTickTimeNanos;
        var rateText = manager.TickRate.ToString("F1");

        if (manager.IsSprinting)
        {
            source.SendSuccess("刻状态 加速跑中");
            source.SendSuccess($"刻速率{rateText}单拍耗时{NanosToMillis(average)} ms");
        }
        else
        {
            //落后判定用平均单拍耗时超过单刻目标 对应原版 nanosecondsPerTick < averageTickTimeNanos
            var status = manager.IsFrozen ? "冻结"
                : manager.NanosecondsPerTick < average ? "落后"
                : "正常";
            source.SendSuccess($"刻状态{status}");
            source.SendSuccess($"刻速率{rateText}单拍耗时{NanosToMillis(average)}ms" +
                $"单刻目标{manager.MillisecondsPerTick:F1}ms");
        }

        var samples = source.Server.TickTimesNanos.ToArray();
        if (samples.Length == 0) return (int)manager.TickRate;
        Array.Sort(samples);
        source.SendSuccess($"单拍分位数p50 {NanosToMillis(samples[samples.Length / 2])} ms " +
            $"p95 {NanosToMillis(samples[(int)(samples.Length * 0.95)])} ms " +
            $"p99 {NanosToMillis(samples[(int)(samples.Length * 0.99)])} ms 样本 {samples.Length}");
        return (int)manager.TickRate;
    }

    //SetTickRate debug tick rate <每秒刻数> 对应原版 setTickingRate
    private static int SetTickRate(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var rate = FloatArgumentType.GetFloat(context, "rate");
        source.Server.TickRate.SetTickRate(rate);
        source.SendSuccess($"每秒刻数已设为{rate:F1}");
        return (int)rate;
    }

    //Step debug tick step [<时间>] 只有冻结状态下才能走刻 对应原版 step
    private static int Step(CommandContext<CommandSourceStack> context, int advance)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (!source.Server.TickRate.StepGameIfPaused(advance))
        {
            source.SendFailure("世界未冻结无法步进先执行 debug tick freeze");
            return 0;
        }
        source.SendSuccess($"已步进{advance}刻");
        return 1;
    }

    //StopStepping debug tick step stop 对应原版 stopStepping
    private static int StopStepping(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (!source.Server.TickRate.StopStepping())
        {
            source.SendFailure("当前没有待执行的步进");
            return 0;
        }
        source.SendSuccess("已停止步进");
        return 1;
    }

    //Sprint debug tick sprint <时间> 加速跑指定刻数 对应原版 sprint
    private static int Sprint(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        var ticks = context.GetArgument<int>("time");
        var interrupted = source.Server.TickRate.RequestGameToSprint(ticks);
        if (interrupted) source.SendSuccess("上一次加速跑已被打断");
        source.SendSuccess($"开始加速跑{ticks}刻");
        return 1;
    }

    //StopSprinting debug tick sprint stop 对应原版 stopSprinting
    private static int StopSprinting(CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return 0;
        if (!source.Server.TickRate.StopSprinting())
        {
            source.SendFailure("当前没有在加速跑");
            return 0;
        }
        source.SendSuccess("已停止加速跑");
        return 1;
    }

    //SetFreeze debug tick freeze|unfreeze 冻结前先收掉加速跑与步进 对应原版 setFreeze
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
        source.SendSuccess(freeze ? "世界已冻结" : "世界已解冻");
        return freeze ? 1 : 0;
    }

    //NanosToMillis 纳秒转毫秒保留一位小数 对应原版 nanosToMilisString
    private static string NanosToMillis(long nanos) => (nanos / 1_000_000.0).ToString("F1");

    //SuggestStrings 从固定候选里挑前缀匹配项补全
    private static Task<Suggestions> SuggestStrings(SuggestionsBuilder builder, params string[] candidates)
    {
        var remaining = builder.Remaining;
        foreach (var candidate in candidates)
            if (candidate.StartsWith(remaining, StringComparison.Ordinal))
                builder.Add(candidate);
        return builder.BuildFuture();
    }

    //--- 公共辅助 ---

    //RequireFakePlayer 取命令源与目标假玩家 缺失时已回执失败
    private static (ServerCommandSource? Source, DebugPlayer? Fake) RequireFakePlayer(
        CommandContext<CommandSourceStack> context)
    {
        var source = RequireSource(context);
        if (source is null) return (null, null);
        var name = StringArgumentType.GetString(context, "name");
        var fake = source.Server.DebugPlayers.Find(name);
        if (fake is null)
        {
            source.SendFailure($"假玩家{name}不存在先用 debug join {name} 创建");
            return (source, null);
        }
        return (source, fake);
    }

    //ResolveEntityId 解析目标实体 id 先按在线玩家名再按整数实体 id
    private static int? ResolveEntityId(ServerCommandSource source, string target)
    {
        var online = source.Server.PlayerList.GetPlayerByName(target);
        if (online is not null) return online.EntityId;
        return int.TryParse(target, out var id) ? id : null;
    }

    //ReadPosition 读三个 double 参数拼成坐标
    private static Vec3 ReadPosition(CommandContext<CommandSourceStack> context, string x, string y, string z)
        => new(DoubleArgumentType.GetDouble(context, x),
            DoubleArgumentType.GetDouble(context, y),
            DoubleArgumentType.GetDouble(context, z));

    //ReadBlockPos 读三个整数参数拼成方块坐标
    private static BlockPos ReadBlockPos(CommandContext<CommandSourceStack> context, string x, string y, string z)
        => new(IntegerArgumentType.GetInteger(context, x),
            IntegerArgumentType.GetInteger(context, y),
            IntegerArgumentType.GetInteger(context, z));

    //DirectionOffset 方向词转单位位移 北是 -Z 南是 +Z 与游戏内一致
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

    //GetBlockPos 坐标参数按执行者位置求绝对坐标 下取整落到方块格
    private static BlockPos GetBlockPos(CommandContext<CommandSourceStack> context, ServerCommandSource source)
    {
        var position = Vec3Argument.GetCoordinates(context, "pos").GetPosition(source);
        return new BlockPos((int)Math.Floor(position.X), (int)Math.Floor(position.Y), (int)Math.Floor(position.Z));
    }

    private static string FormatPos(BlockPos pos) => $"{pos.X} {pos.Y} {pos.Z}";

    private static string FormatPos(Vec3 pos) => $"{pos.X:0.##} {pos.Y:0.##} {pos.Z:0.##}";
}
