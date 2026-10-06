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

//ClientGamePacketListenerImpl client play listener implementation
//Maps to vanilla ClientPacketListener; this round implements the minimal subset for entering the world
//HandleLogin records entityId/gameType and triggers the onJoinWorld callback
//HandleMovePlayer syncs the local player position/orientation; HandleLevelChunkWithLight loads chunks into ClientLevel
//HandleSetHealth/SetExperience update the Player state; HandleForgetLevelChunk unloads chunks
//The remaining 120+ packets are empty implementations, expanded on demand
public sealed class ClientGamePacketListenerImpl : ClientGamePacketListener
{
    private readonly Connection _connection;
    private readonly ClientLevel? _level;
    private readonly Player _player;
    //OnJoinWorld fired once on the Login packet; MinecraftClient switches to GameScreen
    private readonly System.Action? _onJoinWorld;
    private bool _joinNotified;

    public ClientGamePacketListenerImpl(Connection connection, ClientLevel? level, Player player, System.Action? onJoinWorld = null)
    {
        _connection = connection;
        _level = level;
        _player = player;
        _onJoinWorld = onJoinWorld;
    }

    //Explicitly returns Play because the inheritance chain's default Protocol is Configuration
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Play;

    //Inventory client inventory cache; container packet results land here for the GUI and rendering to read
    public ClientInventory Inventory { get; } = new();

    //OnOpenScreen screen callback on open_screen, injected by MinecraftClient
    //The listener only does protocol parsing; the screen layer does not know network packets, and no screen is switched when not injected
    public System.Action<ClientboundOpenScreenPacket>? OnOpenScreen { get; set; }

    //OnContainerClose closes the container screen on container_close, injected by MinecraftClient
    public System.Action? OnContainerClose { get; set; }

    //CommandRoot the command tree root held by the client, sent by ClientboundCommandsPacket
    public CommandNode<CommandSourceStack>? CommandRoot { get; private set; }

    //HandleLogin on entering the world records entityId/gameType and triggers the join callback once
    public void HandleLogin(ClientboundLoginPacket packet)
    {
        _player.GameMode = packet.CommonPlayerSpawnInfo.GameType.Id;
        _player.IsHardcore = packet.Hardcore;
        if (_joinNotified) return;
        _joinNotified = true;
        Log.Info($"Entered world entityId={packet.PlayerId} gameType={packet.CommonPlayerSpawnInfo.GameType.Name}");
        _onJoinWorld?.Invoke();
    }

    //HandleMovePlayer server syncs the player position, maps to vanilla ClientPacketListener.handleMovePlayer
    //The packet components are added to the current value per relatives to restore the absolute position; without this step relative teleports are wrong
    //Immediately reply with the acknowledgment packet and own position; before the acknowledgment the server only accepts orientation, using its recorded target for position
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

    //HandleLevelChunkWithLight loads chunk data into ClientLevel; the render layer subscribes to events and compiles meshes automatically
    public void HandleLevelChunkWithLight(ClientboundLevelChunkWithLightPacket packet)
    {
        if (packet.ChunkData is null) return;
        _level?.LoadChunk(packet.ChunkData);
        //Block entities carried by the chunk are filled into the client block entity table too; the chunk packet is sent only once and blocks are not resent individually
        if (_level is not null && packet.ChunkData is LevelChunk { BlockEntityTags: { Count: > 0 } tags })
            StoreChunkBlockEntities(tags);
        if (packet.LightData is not null && _level is not null)
            ApplyLightData(_level, packet.X, packet.Z, packet.LightData);
    }

    //StoreChunkBlockEntities stores block entities into the client block entity table by the coordinates in their NBT
    //The client builds no block entity objects; it only stores NBT by position for rendering and screens to read
    private void StoreChunkBlockEntities(List<CompoundTag> tags)
    {
        foreach (var tag in tags)
        {
            var pos = new BlockPos(tag.GetIntOr("x", 0), tag.GetIntOr("y", 0), tag.GetIntOr("z", 0));
            _level!.SetBlockEntityData(pos, tag);
        }
    }

    //HandleLightUpdatePacket loads incremental light updates into ClientLevel
    public void HandleLightUpdatePacket(ClientboundLightUpdatePacket packet)
    {
        if (_level is null) return;
        ApplyLightData(_level, packet.X, packet.Z, packet.LightData);
    }

    //ApplyLightData restores the sent light per layer into section storage, maps to vanilla applyLightData
    //Sections with a set mask take the sequential updates data; sections with an empty mask are cleared to all 0; sections with neither keep their old value
    private static void ApplyLightData(ClientLevel level, int chunkX, int chunkZ,
        ClientboundLightUpdatePacketData data)
    {
        ApplyLightLayer(level, chunkX, chunkZ, data.SkyYMask, data.EmptySkyYMask, data.SkyUpdates,
            skyLayer: true);
        ApplyLightLayer(level, chunkX, chunkZ, data.BlockYMask, data.EmptyBlockYMask, data.BlockUpdates,
            skyLayer: false);
    }

    //ApplyLightLayer single-layer restore; section range is the light sections derived from the Overworld default -64..320
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

    //LightMinSectionY/LightSectionCount Overworld light section range, aligning with -4/24 used by chunk decoding
    private const int LightMinSectionY = -5;
    private const int LightSectionCount = 26;

    //HandleForgetLevelChunk unloads the chunk and triggers render layer cleanup to prevent leaks
    //The current packet codec is an object placeholder and the protocol is not registered so it is never received; entry kept
    public void HandleForgetLevelChunk(ClientboundForgetLevelChunkPacket packet) { }

    //HandleSetHealth updates health and hunger
    public void HandleSetHealth(ClientboundSetHealthPacket packet)
    {
        _player.Health = packet.Health;
        _player.FoodLevel = packet.Food;
    }

    //HandleSetExperience updates experience
    public void HandleSetExperience(ClientboundSetExperiencePacket packet)
    {
        _player.XpP = packet.ExperienceProgress;
        _player.XpTotal = packet.TotalExperience;
        _player.XpLevel = packet.ExperienceLevel;
    }

    //HandleChunkBatchStart chunk batch start; no flow control currently, logs only
    public void HandleChunkBatchStart(ClientboundChunkBatchStartPacket packet)
        => Log.Debug("HandleChunkBatchStart");

    //HandleChunkBatchFinished chunk batch finished; no flow control currently, logs only
    public void HandleChunkBatchFinished(ClientboundChunkBatchFinishedPacket packet)
        => Log.Debug($"HandleChunkBatchFinished batchSize={packet.BatchSize}");

    //HandleSetChunkCacheRadius view distance sync; currently uses the local config, callback kept for extension
    public void HandleSetChunkCacheRadius(ClientboundSetChunkCacheRadiusPacket packet)
        => Log.Debug($"HandleSetChunkCacheRadius radius={packet.Radius}");

    //HandleSetChunkCacheCenter view distance center sync; the current player cannot move, ignored
    public void HandleSetChunkCacheCenter(ClientboundSetChunkCacheCenterPacket packet)
        => Log.Debug($"HandleSetChunkCacheCenter ({packet.X},{packet.Z})");

    //The remaining ClientGamePacketListener packets below are empty implementations, expanded on demand
    //HandleAddEntity loads an entity added by the server; orientation bytes are decompressed into angles as in vanilla
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
    //HandleBlockDestruction block breaking crack stage; the stock client has no rendering, it only logs for debugging
    public void HandleBlockDestruction(ClientboundBlockDestructionPacket packet)
        => Log.Debug($"HandleBlockDestruction id={packet.Id} pos={packet.Pos} progress={packet.Progress}");
    public void HandleOpenSignEditor(ClientboundOpenSignEditorPacket packet) { }
    //HandleBlockEntityData loads block entity state into the client world; empty NBT means the block entity there was removed
    public void HandleBlockEntityData(ClientboundBlockEntityDataPacket packet)
    {
        _level?.SetBlockEntityData(packet.Pos, packet.Tag);
        Log.Debug($"HandleBlockEntityData pos={packet.Pos} type={packet.BlockEntityTypeId}");
    }

    //HandleBlockEvent block event (chest open/close, piston extend/retract, etc.); no visual representation yet, logs only
    public void HandleBlockEvent(ClientboundBlockEventPacket packet)
        => Log.Debug($"HandleBlockEvent pos={packet.Pos} b0={packet.B0} b1={packet.B1} block={packet.BlockId}");
    public void HandleBlockUpdate(ClientboundBlockUpdatePacket packet)
    {
        //Client-side block placement must leave a trace, otherwise "the server did not send" and "the client did not apply" cannot be told apart
        Log.Debug($"Redstone block update received {packet.Pos} state={packet.BlockState} block={BlockStateRegistry.GetState(packet.BlockState).Owner.Id}");
        _level?.SetBlockState(packet.Pos, BlockStateRegistry.GetState(packet.BlockState));
    }
    public void HandleSystemChat(ClientboundSystemChatPacket packet) { }
    public void HandlePlayerChat(ClientboundPlayerChatPacket packet) { }
    public void HandleDisguisedChat(ClientboundDisguisedChatPacket packet) { }
    public void HandleDeleteChat(ClientboundDeleteChatPacket packet) { }
    //HandleChunkBlocksUpdate batched block update within a section; each entry is (stateId << 12) | a 12-bit in-section offset
    //Maps to runUpdates in vanilla ClientPacketListener.handleChunkBlocksUpdate
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
    //HandleContainerClose the server proactively closes the container screen; received when the player walks away or the block is broken
    public void HandleContainerClose(ClientboundContainerClosePacket packet) => OnContainerClose?.Invoke();
    //HandleContainerContent full container contents; builds menu slots and records the cursor
    public void HandleContainerContent(ClientboundContainerSetContentPacket packet)
        => Inventory.SetContent(packet.ContainerId, packet.StateId, packet.Items, packet.CarriedItem);

    public void HandleMountScreenOpen(ClientboundMountScreenOpenPacket packet) { }
    public void HandleContainerSetData(ClientboundContainerSetDataPacket packet) { }

    //HandleContainerSetSlot single-slot change; packets for a non-current menu are discarded
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
    //HandleMoveEntity relative displacement and rotation packet; displacement is restored at 1/4096 block units
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
    //HandleSetEntityData records entity metadata; the dropped item's item stack is synced through it
    public void HandleSetEntityData(ClientboundSetEntityDataPacket packet)
        => _level?.SetEntityData(packet.Id, packet.PackedItems);
    //HandleSetEntityMotion records entity velocity for interpolation
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
    //HandleEntityPositionSync server-authoritative position sync; overwrites the entity position and orientation entirely
    //The entity position baseline is reset with it, so later incremental displacements are relative to the new position, maps to vanilla handleEntityPositionSync
    public void HandleEntityPositionSync(ClientboundEntityPositionSyncPacket packet)
        => _level?.SetEntityPosition(packet.Id, packet.Position, packet.YRot, packet.XRot, packet.OnGround);
    //HandleTeleportEntity teleport packet overwrites the entity position and orientation entirely
    public void HandleTeleportEntity(ClientboundTeleportEntityPacket packet)
        => _level?.SetEntityPosition(packet.Id, packet.Position, packet.YRot, packet.XRot, packet.OnGround);
    //HandleTickingState tick rate and frozen state; the local world advances at the server tick rate, and while frozen entity animation stops
    public void HandleTickingState(ClientboundTickingStatePacket packet)
        => _level?.SetTickingState(packet.TickRate, packet.IsFrozen);
    //HandleTickingStep step ticks while frozen; after these ticks the local world returns to frozen
    public void HandleTickingStep(ClientboundTickingStepPacket packet)
        => _level?.SetTickingStep(packet.TickSteps);
    //HandlePongResponse the server's reply to the client's ping_request; vanilla uses it to compute round-trip latency
    //This client does not measure latency and discards it on receipt, but it must be implemented or Play-phase pong cannot be decoded
    public void HandlePongResponse(ClientboundPongResponsePacket packet) { }
    //HandleUpdateAttributes records entity attributes; base values and modifiers are applied to the client entity
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
    //HandleOpenScreen the server requests opening a menu screen; container contents are filled in afterwards by container_set_content
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
    //HandleSetCursorItem cursor item change; slot -1 is used
    public void HandleSetCursorItem(ClientboundSetCursorItemPacket packet)
        => Inventory.SetSlot(AbstractContainerMenu.CarriedSlotIndex, packet.Contents);

    //HandleSetPlayerInventory single player inventory slot change; the slot number is the inventory index
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

    //The following are inherited from ClientCommonPacketListener; the current server does not send them, empty implementation
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

    //Inherited from ClientCookiePacketListener
    public void HandleCookieRequest(ClientboundCookieRequestPacket packet) { }

    public void OnDisconnect(string reason)
        => Log.Info($"play phase disconnect reason={reason}");
}
