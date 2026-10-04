using NetCraft.Commands;
using NetCraft.Commands.Tree;
using NetCraft.Game.Client.Inventory;
using NetCraft.Game.Client.Level;
using NetCraft.Network.Protocol.Common;
using NetCraft.Network.Protocol.Cookie;
using NetCraft.Network.Protocol.Ping;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientGamePacketListenerImpl 客户端 play 监听器实现
//对应原版 ClientPacketListener 本轮实现进入世界最小子集
//HandleLogin 记录 entityId/gameType 触发 onJoinWorld 回调
//HandleMovePlayer 同步本地玩家位置朝向 HandleLevelChunkWithLight 装载区块到 ClientLevel
//HandleSetHealth/SetExperience 更新 Player 状态 HandleForgetLevelChunk 卸载区块
//其余 120+ 包空实现按需扩展
public sealed class ClientGamePacketListenerImpl : ClientGamePacketListener
{
    private readonly Connection _connection;
    private readonly ClientLevel? _level;
    private readonly Player _player;
    //OnJoinWorld 收到 Login 包触发一次由 MinecraftClient 切 GameScreen
    private readonly System.Action? _onJoinWorld;
    private bool _joinNotified;

    public ClientGamePacketListenerImpl(Connection connection, ClientLevel? level, Player player, System.Action? onJoinWorld = null)
    {
        _connection = connection;
        _level = level;
        _player = player;
        _onJoinWorld = onJoinWorld;
    }

    //显式返回 Play 因继承链默认 Protocol 是 Configuration
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Play;

    //Inventory 客户端物品栏缓存 容器包的处理结果落在这里供 GUI 与渲染读取
    public ClientInventory Inventory { get; } = new();

    //OnOpenScreen 收到 open_screen 时的界面回调 由 MinecraftClient 注入
    //监听器只做协议解析 界面层不认识网络包 未注入时不切屏
    public System.Action<ClientboundOpenScreenPacket>? OnOpenScreen { get; set; }

    //OnContainerClose 收到 container_close 时关闭容器界面 由 MinecraftClient 注入
    public System.Action? OnContainerClose { get; set; }

    //CommandRoot 客户端持有的命令树根 由 ClientboundCommandsPacket 下发
    public CommandNode<CommandSourceStack>? CommandRoot { get; private set; }

    //HandleLogin 进入世界记录 entityId/gameType 触发一次加入回调
    public void HandleLogin(ClientboundLoginPacket packet)
    {
        _player.GameMode = packet.CommonPlayerSpawnInfo.GameType.Id;
        _player.IsHardcore = packet.Hardcore;
        if (_joinNotified) return;
        _joinNotified = true;
        Log.Info($"Entered world entityId={packet.PlayerId} gameType={packet.CommonPlayerSpawnInfo.GameType.Name}");
        _onJoinWorld?.Invoke();
    }

    //HandleMovePlayer 服务器同步玩家位置 对应原版 ClientPacketListener.handleMovePlayer
    //包内分量按 relatives 叠加自身当前值复原绝对位置 缺这一步相对传送会算错
    //应用后立刻回确认包与自身位置 服务端在确认前只接受朝向 位置以它记录的目标为准
    public void HandleMovePlayer(ClientboundPlayerPositionPacket packet)
    {
        var relatives = new HashSet<RelativeFlag>(RelativeFlags.Unpack(packet.Relatives));
        var x = relatives.Contains(RelativeFlag.X) ? _player.Pos.X + packet.X : packet.X;
        var y = relatives.Contains(RelativeFlag.Y) ? _player.Pos.Y + packet.Y : packet.Y;
        var z = relatives.Contains(RelativeFlag.Z) ? _player.Pos.Z + packet.Z : packet.Z;
        var yRot = relatives.Contains(RelativeFlag.YRot) ? _player.YRot + packet.YRot : packet.YRot;
        var xRot = relatives.Contains(RelativeFlag.XRot) ? _player.XRot + packet.XRot : packet.XRot;
        _player.Pos = new Vec3(x, y, z);
        _player.YRot = yRot;
        _player.XRot = xRot;
        _connection.Send(new ServerboundAcceptTeleportationPacket(packet.Id));
        _connection.Send(new ServerboundMovePlayerPacket(x, y, z, yRot, xRot, false, false, true, true));
    }

    //HandleLevelChunkWithLight 装载区块数据到 ClientLevel 渲染层订阅事件自动编译 mesh
    public void HandleLevelChunkWithLight(ClientboundLevelChunkWithLightPacket packet)
    {
        if (packet.ChunkData is null) return;
        _level?.LoadChunk(packet.ChunkData);
        //区块自带的方块实体一并填进客户端方块实体表 区块包只发一次不再逐块补
        if (_level is not null && packet.ChunkData is LevelChunk { BlockEntityTags: { Count: > 0 } tags })
            StoreChunkBlockEntities(tags);
        if (packet.LightData is not null && _level is not null)
            ApplyLightData(_level, packet.X, packet.Z, packet.LightData);
    }

    //StoreChunkBlockEntities 按 NBT 里的坐标把方块实体存进客户端方块实体表
    //客户端不建方块实体对象 只按坐标存 NBT 供渲染与界面读取
    private void StoreChunkBlockEntities(List<CompoundTag> tags)
    {
        foreach (var tag in tags)
        {
            var pos = new BlockPos(tag.GetIntOr("x", 0), tag.GetIntOr("y", 0), tag.GetIntOr("z", 0));
            _level!.SetBlockEntityData(pos, tag);
        }
    }

    //HandleLightUpdatePacket 光照增量更新装载到 ClientLevel
    public void HandleLightUpdatePacket(ClientboundLightUpdatePacket packet)
    {
        if (_level is null) return;
        ApplyLightData(_level, packet.X, packet.Z, packet.LightData);
    }

    //ApplyLightData 把下发光照按层还原到区段存储对应原版 applyLightData
    //掩码置位的区段取 updates 顺序数据 空掩码置位的区段清为全 0 两者皆无的区段保持原值
    private static void ApplyLightData(ClientLevel level, int chunkX, int chunkZ,
        ClientboundLightUpdatePacketData data)
    {
        ApplyLightLayer(level, chunkX, chunkZ, data.SkyYMask, data.EmptySkyYMask, data.SkyUpdates,
            skyLayer: true);
        ApplyLightLayer(level, chunkX, chunkZ, data.BlockYMask, data.EmptyBlockYMask, data.BlockUpdates,
            skyLayer: false);
    }

    //ApplyLightLayer 单层还原 区段范围按主世界默认 -64..320 派生的光照区段
    private static void ApplyLightLayer(ClientLevel level, int chunkX, int chunkZ,
        byte[] mask, byte[] emptyMask, byte[][] updates, bool skyLayer)
    {
        var updateIndex = 0;
        for (var sectionIndex = 0; sectionIndex < LightSectionCount; sectionIndex++)
        {
            if (ClientboundLightUpdatePacketData.IsSet(mask, sectionIndex))
            {
                if (updateIndex >= updates.Length) break;
                var layer = new DataLayer(updates[updateIndex++]);
                level.SetLightLayer(new SectionPos(chunkX, LightMinSectionY + sectionIndex, chunkZ),
                    skyLayer, layer);
            }
            else if (ClientboundLightUpdatePacketData.IsSet(emptyMask, sectionIndex))
            {
                level.SetLightLayer(new SectionPos(chunkX, LightMinSectionY + sectionIndex, chunkZ),
                    skyLayer, new DataLayer());
            }
        }
    }

    //LightMinSectionY/LightSectionCount 主世界光照区段范围 对齐区块解码用的 -4/24
    private const int LightMinSectionY = -5;
    private const int LightSectionCount = 26;

    //HandleForgetLevelChunk 卸载区块触发渲染层清理防泄漏
    //当前包 codec 是 object 占位未注册协议收不到保留入口
    public void HandleForgetLevelChunk(ClientboundForgetLevelChunkPacket packet) { }

    //HandleSetHealth 更新生命饥饿
    public void HandleSetHealth(ClientboundSetHealthPacket packet)
    {
        _player.Health = packet.Health;
        _player.FoodLevel = packet.Food;
    }

    //HandleSetExperience 更新经验
    public void HandleSetExperience(ClientboundSetExperiencePacket packet)
    {
        _player.XpP = packet.ExperienceProgress;
        _player.XpTotal = packet.TotalExperience;
        _player.XpLevel = packet.ExperienceLevel;
    }

    //HandleChunkBatchStart 区块批次开始当前无流控记录日志
    public void HandleChunkBatchStart(ClientboundChunkBatchStartPacket packet)
        => Log.Debug("HandleChunkBatchStart");

    //HandleChunkBatchFinished 区块批次完成当前无流控记录日志
    public void HandleChunkBatchFinished(ClientboundChunkBatchFinishedPacket packet)
        => Log.Debug($"HandleChunkBatchFinished batchSize={packet.BatchSize}");

    //HandleSetChunkCacheRadius 视距同步当前用本地配置留回调扩展
    public void HandleSetChunkCacheRadius(ClientboundSetChunkCacheRadiusPacket packet)
        => Log.Debug($"HandleSetChunkCacheRadius radius={packet.Radius}");

    //HandleSetChunkCacheCenter 视距中心同步当前玩家不可移动忽略
    public void HandleSetChunkCacheCenter(ClientboundSetChunkCacheCenterPacket packet)
        => Log.Debug($"HandleSetChunkCacheCenter ({packet.X},{packet.Z})");

    //以下 ClientGamePacketListener 其余包空实现按需扩展
    //HandleAddEntity 装载服务端新增实体 朝向字节按原版解压缩成角度
    public void HandleAddEntity(ClientboundAddEntityPacket packet)
    {
        _level?.AddEntity(packet.Id, packet.Kind,
            new Vec3(packet.X, packet.Y, packet.Z),
            Mth.UnpackDegrees(packet.YRot), Mth.UnpackDegrees(packet.XRot),
            packet.Movement, false);
        Log.Debug($"HandleAddEntity id={packet.Id} type={packet.Kind.Id}");
    }

    public void HandleAddObjective(ClientboundSetObjectivePacket packet) { }
    public void HandleAnimate(ClientboundAnimatePacket packet) { }
    public void HandleHurtAnimation(ClientboundHurtAnimationPacket packet) { }
    public void HandleAwardStats(ClientboundAwardStatsPacket packet) { }
    public void HandleRecipeBookAdd(ClientboundRecipeBookAddPacket packet) { }
    public void HandleRecipeBookRemove(ClientboundRecipeBookRemovePacket packet) { }
    public void HandleRecipeBookSettings(ClientboundRecipeBookSettingsPacket packet) { }
    //HandleBlockDestruction 方块破坏裂纹阶段 自带客户端无渲染 只记录便于联调排查
    public void HandleBlockDestruction(ClientboundBlockDestructionPacket packet)
        => Log.Debug($"HandleBlockDestruction id={packet.Id} pos={packet.Pos} progress={packet.Progress}");
    public void HandleOpenSignEditor(ClientboundOpenSignEditorPacket packet) { }
    //HandleBlockEntityData 方块实体状态装载进客户端世界 空 NBT 表示该位置方块实体已移除
    public void HandleBlockEntityData(ClientboundBlockEntityDataPacket packet)
    {
        _level?.SetBlockEntityData(packet.Pos, packet.Tag);
        Log.Debug($"HandleBlockEntityData pos={packet.Pos} type={packet.BlockEntityTypeId}");
    }

    //HandleBlockEvent 方块事件(箱子开合/活塞伸缩等) 当前无对应视觉表现只记录日志
    public void HandleBlockEvent(ClientboundBlockEventPacket packet)
        => Log.Debug($"HandleBlockEvent pos={packet.Pos} b0={packet.B0} b1={packet.B1} block={packet.BlockId}");
    public void HandleBlockUpdate(ClientboundBlockUpdatePacket packet)
    {
        //客户端落地方块包要有痕迹 否则分不清"服务端没发"和"客户端没应用"
        Log.Debug($"Redstone block update received {packet.Pos} state={packet.BlockState} block={BlockStateRegistry.GetState(packet.BlockState).Owner.Id}");
        _level?.SetBlockState(packet.Pos, BlockStateRegistry.GetState(packet.BlockState));
    }
    public void HandleSystemChat(ClientboundSystemChatPacket packet) { }
    public void HandlePlayerChat(ClientboundPlayerChatPacket packet) { }
    public void HandleDisguisedChat(ClientboundDisguisedChatPacket packet) { }
    public void HandleDeleteChat(ClientboundDeleteChatPacket packet) { }
    //HandleChunkBlocksUpdate 段内批量方块更新 每项 (状态id << 12) | 段内 12 位偏移
    //对应原版 ClientPacketListener.handleChunkBlocksUpdate 的 runUpdates
    public void HandleChunkBlocksUpdate(ClientboundSectionBlocksUpdatePacket packet)
    {
        if (_level is null) return;
        var baseX = SectionPos.SectionToBlockCoord(packet.SectionPos.X);
        var baseY = SectionPos.SectionToBlockCoord(packet.SectionPos.Y);
        var baseZ = SectionPos.SectionToBlockCoord(packet.SectionPos.Z);
        foreach (var packed in packet.PackedChanges)
        {
            var pos = new BlockPos(
                baseX + SectionPos.RelativeX(packed),
                baseY + SectionPos.RelativeY(packed),
                baseZ + SectionPos.RelativeZ(packed));
            _level.SetBlockState(pos, BlockStateRegistry.GetState((int)(packed >> 12)));
        }
    }
    public void HandleMapItemData(ClientboundMapItemDataPacket packet) { }
    //HandleContainerClose 服务端主动关闭容器界面 玩家走远或方块被拆时收到
    public void HandleContainerClose(ClientboundContainerClosePacket packet) => OnContainerClose?.Invoke();
    //HandleContainerContent 容器全量内容 建立菜单槽位并记录光标
    public void HandleContainerContent(ClientboundContainerSetContentPacket packet)
        => Inventory.SetContent(packet.ContainerId, packet.StateId, packet.Items, packet.CarriedItem);

    public void HandleMountScreenOpen(ClientboundMountScreenOpenPacket packet) { }
    public void HandleContainerSetData(ClientboundContainerSetDataPacket packet) { }

    //HandleContainerSetSlot 单槽变更 非当前菜单的包直接丢弃
    public void HandleContainerSetSlot(ClientboundContainerSetSlotPacket packet)
    {
        if (packet.ContainerId != Inventory.ContainerId) return;
        Inventory.SetSlot(packet.Slot, packet.Stack);
    }
    public void HandleEntityEvent(ClientboundEntityEventPacket packet) { }
    public void HandleEntityLinkPacket(ClientboundSetEntityLinkPacket packet) { }
    public void HandleSetEntityPassengersPacket(ClientboundSetPassengersPacket packet) { }
    public void HandleExplosion(ClientboundExplodePacket packet) { }
    public void HandleGameEvent(ClientboundGameEventPacket packet) { }
    public void HandleChunksBiomes(ClientboundChunksBiomesPacket packet) { }
    public void HandleLevelEvent(ClientboundLevelEventPacket packet) { }
    //HandleMoveEntity 相对位移与旋转包 位移按 1/4096 格还原
    public void HandleMoveEntity(ClientboundMoveEntityPacket packet)
    {
        _level?.MoveEntity(packet.EntityId,
            packet.Xa / 4096.0, packet.Ya / 4096.0, packet.Za / 4096.0,
            packet.HasRot ? Mth.UnpackDegrees(packet.YRot) : null,
            packet.HasRot ? Mth.UnpackDegrees(packet.XRot) : null,
            packet.OnGround);
    }
    public void HandleMinecartAlongTrack(ClientboundMoveMinecartPacket packet) { }
    public void HandleRotatePlayer(ClientboundPlayerRotationPacket packet) { }
    public void HandleParticleEvent(ClientboundLevelParticlesPacket packet) { }
    public void HandlePlayerAbilities(ClientboundPlayerAbilitiesPacket packet) { }
    public void HandleGameRuleValues(ClientboundGameRuleValuesPacket packet) { }
    public void HandlePlayerInfoRemove(ClientboundPlayerInfoRemovePacket packet) { }
    public void HandlePlayerInfoUpdate(ClientboundPlayerInfoUpdatePacket packet) { }
    public void HandleRemoveEntities(ClientboundRemoveEntitiesPacket packet)
        => _level?.RemoveEntities(packet.EntityIds);
    public void HandleRemoveMobEffect(ClientboundRemoveMobEffectPacket packet) { }
    public void HandleRespawn(ClientboundRespawnPacket packet) { }
    public void HandleRotateMob(ClientboundRotateHeadPacket packet) { }
    public void HandleSetHeldSlot(ClientboundSetHeldSlotPacket packet) { }
    public void HandleSetDisplayObjective(ClientboundSetDisplayObjectivePacket packet) { }
    //HandleSetEntityData 记录实体元数据 掉落物的物品栈靠它同步
    public void HandleSetEntityData(ClientboundSetEntityDataPacket packet)
        => _level?.SetEntityData(packet.Id, packet.PackedItems);
    //HandleSetEntityMotion 记录实体速度供插值
    public void HandleSetEntityMotion(ClientboundSetEntityMotionPacket packet)
        => _level?.SetEntityMotion(packet.Id, packet.Movement);
    public void HandleSetEquipment(ClientboundSetEquipmentPacket packet) { }
    public void HandleSetPlayerTeamPacket(ClientboundSetPlayerTeamPacket packet) { }
    public void HandleSetScore(ClientboundSetScorePacket packet) { }
    public void HandleResetScore(ClientboundResetScorePacket packet) { }
    public void HandleSetSpawn(ClientboundSetDefaultSpawnPositionPacket packet) { }
    public void HandleSetTime(ClientboundSetTimePacket packet) { }
    public void HandleSoundEvent(ClientboundSoundPacket packet) => Log.Debug($"Sound received {packet.Sound.Location} source={packet.Source}");
    public void HandleSoundEntityEvent(ClientboundSoundEntityPacket packet) => Log.Debug($"Entity sound received {packet.Sound.Location} entityId={packet.Id}");
    public void HandleTakeItemEntity(ClientboundTakeItemEntityPacket packet) { }
    //HandleEntityPositionSync 服务端权威位置同步 整段覆盖实体位置与朝向
    //实体位置基准随它重置 之后收到的增量位移才是相对新位置的 对应原版 handleEntityPositionSync
    public void HandleEntityPositionSync(ClientboundEntityPositionSyncPacket packet)
        => _level?.SetEntityPosition(packet.Id, packet.Position, packet.YRot, packet.XRot, packet.OnGround);
    //HandleTeleportEntity 传送包整段覆盖实体位置与朝向
    public void HandleTeleportEntity(ClientboundTeleportEntityPacket packet)
        => _level?.SetEntityPosition(packet.Id, packet.Position, packet.YRot, packet.XRot, packet.OnGround);
    //HandleTickingState 刻速率与冻结状态 本地世界按服务端刻率推进 冻结时停住实体动画随之停住
    public void HandleTickingState(ClientboundTickingStatePacket packet)
        => _level?.SetTickingState(packet.TickRate, packet.IsFrozen);
    //HandleTickingStep 冻结下的步进刻数 走完这些刻本地世界仍回到冻结
    public void HandleTickingStep(ClientboundTickingStepPacket packet)
        => _level?.SetTickingStep(packet.TickSteps);
    //HandlePongResponse 服务端对客户端 ping_request 的应答 原版拿它算往返延迟
    //本作客户端不打延迟统计 收到即丢 但必须实现否则 Play 阶段 pong 解不出来
    public void HandlePongResponse(ClientboundPongResponsePacket packet) { }
    //HandleUpdateAttributes 记录实体属性 基值与修饰符都覆盖到客户端实体上
    public void HandleUpdateAttributes(ClientboundUpdateAttributesPacket packet)
        => _level?.SetEntityAttributes(packet.EntityId, packet.Attributes);
    public void HandleUpdateMobEffect(ClientboundUpdateMobEffectPacket packet) { }
    public void HandlePlayerCombatEnd(ClientboundPlayerCombatEndPacket packet) { }
    public void HandlePlayerCombatEnter(ClientboundPlayerCombatEnterPacket packet) { }
    public void HandlePlayerCombatKill(ClientboundPlayerCombatKillPacket packet) { }
    public void HandleChangeDifficulty(ClientboundChangeDifficultyPacket packet) { }
    public void HandleSetCamera(ClientboundSetCameraPacket packet) { }
    public void HandleInitializeBorder(ClientboundInitializeBorderPacket packet) { }
    public void HandleSetBorderLerpSize(ClientboundSetBorderLerpSizePacket packet) { }
    public void HandleSetBorderSize(ClientboundSetBorderSizePacket packet) { }
    public void HandleSetBorderWarningDelay(ClientboundSetBorderWarningDelayPacket packet) { }
    public void HandleSetBorderWarningDistance(ClientboundSetBorderWarningDistancePacket packet) { }
    public void HandleSetBorderCenter(ClientboundSetBorderCenterPacket packet) { }
    public void HandleTabListCustomisation(ClientboundTabListPacket packet) { }
    public void HandleBossUpdate(ClientboundBossEventPacket packet) { }
    public void HandleItemCooldown(ClientboundCooldownPacket packet) { }
    public void HandleMoveVehicle(ClientboundMoveVehiclePacket packet) { }
    public void HandleUpdateAdvancementsPacket(ClientboundUpdateAdvancementsPacket packet) { }
    public void HandleSelectAdvancementsTab(ClientboundSelectAdvancementsTabPacket packet) { }
    public void HandlePlaceRecipe(ClientboundPlaceGhostRecipePacket packet) { }
    public void HandleCommands(ClientboundCommandsPacket packet) => CommandRoot = packet.Root;
    public void HandleStopSoundEvent(ClientboundStopSoundPacket packet) => Log.Debug($"Stop sound received name={packet.Name} source={packet.Source}");
    public void HandleCommandSuggestions(ClientboundCommandSuggestionsPacket packet) { }
    public void HandleUpdateRecipes(ClientboundUpdateRecipesPacket packet) { }
    public void HandleLookAt(ClientboundPlayerLookAtPacket packet) { }
    public void HandleTagQueryPacket(ClientboundTagQueryPacket packet) { }
    public void HandleOpenBook(ClientboundOpenBookPacket packet) { }
    //HandleOpenScreen 服务端要求打开菜单界面 容器内容随后由 container_set_content 补齐
    public void HandleOpenScreen(ClientboundOpenScreenPacket packet) => OnOpenScreen?.Invoke(packet);
    public void HandleMerchantOffers(ClientboundMerchantOffersPacket packet) { }
    public void HandleSetSimulationDistance(ClientboundSetSimulationDistancePacket packet) { }
    public void HandleBlockChangedAck(ClientboundBlockChangedAckPacket packet) { }
    public void SetActionBarText(ClientboundSetActionBarTextPacket packet) { }
    public void SetSubtitleText(ClientboundSetSubtitleTextPacket packet) { }
    public void SetTitleText(ClientboundSetTitleTextPacket packet) { }
    public void SetTitlesAnimation(ClientboundSetTitlesAnimationPacket packet) { }
    public void HandleTitlesClear(ClientboundClearTitlesPacket packet) { }
    public void HandleServerData(ClientboundServerDataPacket packet) { }
    public void HandleCustomChatCompletions(ClientboundCustomChatCompletionsPacket packet) { }
    public void HandleBundlePacket(ClientboundBundlePacket packet) { }
    public void HandleDamageEvent(ClientboundDamageEventPacket packet) { }
    public void HandleConfigurationStart(ClientboundStartConfigurationPacket packet) { }
    public void HandleDebugSample(ClientboundDebugSamplePacket packet) { }
    public void HandleProjectilePowerPacket(ClientboundProjectilePowerPacket packet) { }
    //HandleSetCursorItem 光标物品变更 槽号用 -1
    public void HandleSetCursorItem(ClientboundSetCursorItemPacket packet)
        => Inventory.SetSlot(AbstractContainerMenu.CarriedSlotIndex, packet.Contents);

    //HandleSetPlayerInventory 玩家物品栏单槽变更 槽号是物品栏下标
    public void HandleSetPlayerInventory(ClientboundSetPlayerInventoryPacket packet)
        => Inventory.SetPlayerSlot(packet.Slot, packet.Contents);
    public void HandleTestInstanceBlockStatus(ClientboundTestInstanceBlockStatus packet) { }
    public void HandleWaypoint(ClientboundTrackedWaypointPacket packet) { }
    public void HandleDebugChunkValue(ClientboundDebugChunkValuePacket packet) { }
    public void HandleDebugBlockValue(ClientboundDebugBlockValuePacket packet) { }
    public void HandleDebugEntityValue(ClientboundDebugEntityValuePacket packet) { }
    public void HandleDebugEvent(ClientboundDebugEventPacket packet) { }
    public void HandleGameTestHighlightPos(ClientboundGameTestHighlightPosPacket packet) { }
    public void HandleLowDiskSpaceWarning(ClientboundLowDiskSpaceWarningPacket packet) { }

    //以下继承自 ClientCommonPacketListener 当前服务器不下发空实现
    public void HandleKeepAlive(ClientboundKeepAlivePacket packet) { }
    public void HandlePing(ClientboundPingPacket packet) { }
    public void HandleCustomPayload(ClientboundCustomPayloadPacket packet) { }
    public void HandleDisconnect(ClientboundDisconnectPacket packet)
        => Log.Warning($"play phase disconnected {packet.Reason}");
    public void HandleResourcePackPush(ClientboundResourcePackPushPacket packet) { }
    public void HandleResourcePackPop(ClientboundResourcePackPopPacket packet) { }
    public void HandleUpdateTags(ClientboundUpdateTagsPacket packet) { }
    public void HandleStoreCookie(ClientboundStoreCookiePacket packet) { }
    public void HandleTransfer(ClientboundTransferPacket packet) { }
    public void HandleCustomReportDetails(ClientboundCustomReportDetailsPacket packet) { }
    public void HandleServerLinks(ClientboundServerLinksPacket packet) { }
    public void HandleClearDialog(ClientboundClearDialogPacket packet) { }
    public void HandleShowDialog(ClientboundShowDialogPacket packet) { }

    //继承自 ClientCookiePacketListener
    public void HandleCookieRequest(ClientboundCookieRequestPacket packet) { }

    public void OnDisconnect(string reason)
        => Log.Info($"play phase disconnect reason={reason}");
}
