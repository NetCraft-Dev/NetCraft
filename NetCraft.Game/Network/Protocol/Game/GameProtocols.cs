using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Game.Network.Protocol.Game;

//GameProtocols play 协议注册
//对应原版 net.minecraft.network.protocol.game.GameProtocols
//ID 由 GamePacketTypes 按原版注册顺序严格对齐 26.2 保证真实客户端可解码
//clientbound 注册玩家进世界必需包 + common 心跳/断开 clientbound 由服务端发送
//serverbound 注册真实客户端进 Play 后主动发送的包（move/chunk ack/player_loaded 等）
//未注册的包解码报"未知包"被丢弃不影响连接
public static class GameProtocols
{
    //ServerboundTemplate SERVERBOUND play 协议模板
    public static readonly SimpleUnboundProtocol<ServerGamePacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerGamePacketListener>(
            ConnectionProtocol.Play, FlowDirection.Serverbound)
            .AddPacket(GamePacketTypes.ServerboundAcceptTeleportation, ServerboundAcceptTeleportationPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundAttack, ServerboundAttackPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundInteract, ServerboundInteractPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChangeGameMode, ServerboundChangeGameModePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChatAck, ServerboundChatAckPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChat, ServerboundChatPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChatCommand, ServerboundChatCommandPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChatCommandSigned, ServerboundChatCommandSignedPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChatSessionUpdate, ServerboundChatSessionUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundClientCommand, ServerboundClientCommandPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundClientTickEnd, ServerboundClientTickEndPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundCommandSuggestion, ServerboundCommandSuggestionPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundClientInformation, ServerboundClientInformationPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChunkBatchReceived, ServerboundChunkBatchReceivedPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundConfigurationAcknowledged, ServerboundConfigurationAcknowledgedPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundContainerClick, ServerboundContainerClickPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundContainerClose, ServerboundContainerClosePacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundCustomPayload, ServerboundCustomPayloadPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundKeepAlive, ServerboundKeepAlivePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundMovePlayerPos, ServerboundMovePlayerPacket.PosStreamCodec)
            .AddPacket(GamePacketTypes.ServerboundMovePlayerPosRot, ServerboundMovePlayerPacket.PosRotStreamCodec)
            .AddPacket(GamePacketTypes.ServerboundMovePlayerRot, ServerboundMovePlayerPacket.RotStreamCodec)
            .AddPacket(GamePacketTypes.ServerboundMovePlayerStatusOnly, ServerboundMovePlayerPacket.StatusOnlyStreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPlayerAction, ServerboundPlayerActionPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPlayerCommand, ServerboundPlayerCommandPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPlayerInput, ServerboundPlayerInputPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPlayerLoaded, ServerboundPlayerLoadedPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPlayerAbilities, ServerboundPlayerAbilitiesPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPickItemFromBlock, ServerboundPickItemFromBlockPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPickItemFromEntity, ServerboundPickItemFromEntityPacket.StreamCodec)
            //ping_request 由客户端 PingDebugMonitor 周期性发 服务端回 pong_response 原版 play 协议 id 38
            .AddPacketCommon(GamePacketTypes.ServerboundPingRequest, ServerboundPingRequestPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundPong, ServerboundPongPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSetCarriedItem, ServerboundSetCarriedItemPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSetCreativeModeSlot, ServerboundSetCreativeModeSlotPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundRecipeBookChangeSettings, ServerboundRecipeBookChangeSettingsPacket.StreamCodec)
            //S4.186 补齐常见交互类 serverbound 包 原版客户端打开界面操作时会发 不注册会刷未知包告警
            .AddPacket(GamePacketTypes.ServerboundRecipeBookSeenRecipe, ServerboundRecipeBookSeenRecipePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundRenameItem, ServerboundRenameItemPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSelectTrade, ServerboundSelectTradePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSetBeacon, ServerboundSetBeaconPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundEditBook, ServerboundEditBookPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSignUpdate, ServerboundSignUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPlaceRecipe, ServerboundPlaceRecipePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundChangeDifficulty, ServerboundChangeDifficultyPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundLockDifficulty, ServerboundLockDifficultyPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundContainerButtonClick, ServerboundContainerButtonClickPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundContainerSlotStateChanged, ServerboundContainerSlotStateChangedPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSpectatorAction, ServerboundSpectatorActionPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundMoveVehicle, ServerboundMoveVehiclePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundTeleportToEntity, ServerboundTeleportToEntityPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSeenAdvancements, ServerboundSeenAdvancementsPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundPaddleBoat, ServerboundPaddleBoatPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundDebugSubscriptionRequest, ServerboundDebugSubscriptionRequestPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundResourcePack, ServerboundResourcePackPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundCustomClickAction, ServerboundCustomClickActionPacket.StreamCodec)
            .AddPacketCommon(CookiePacketTypes.ServerboundCookieResponse, ServerboundCookieResponsePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSwing, ServerboundSwingPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundUseItemOn, ServerboundUseItemOnPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundUseItem, ServerboundUseItemPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerGamePacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND play 协议模板
    //S3 加入 chunk 发送链路注册 Login/PlayerInfo/SystemChat 外补齐 SendJoinPackets 全序列与区块包
    //S4 对齐 26.2 包 ID 补 common 心跳/断开保证真实客户端可解码
    //C3 补容器与装备包注册
    public static readonly SimpleUnboundProtocol<ClientGamePacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientGamePacketListener>(
            ConnectionProtocol.Play, FlowDirection.Clientbound)
            .AddPacket(GamePacketTypes.ClientboundAddEntity, ClientboundAddEntityPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundAnimate, ClientboundAnimatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundBlockChangedAck, ClientboundBlockChangedAckPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundBlockDestruction, ClientboundBlockDestructionPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundBlockEntityData, ClientboundBlockEntityDataPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundBlockEvent, ClientboundBlockEventPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundBlockUpdate, ClientboundBlockUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundChunkBatchFinished, ClientboundChunkBatchFinishedPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundChunkBatchStart, ClientboundChunkBatchStartPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundChunksBiomes, ClientboundChunksBiomesPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundClearTitles, ClientboundClearTitlesPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundCommandSuggestions, ClientboundCommandSuggestionsPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundCommands, ClientboundCommandsPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundContainerClose, ClientboundContainerClosePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundContainerSetContent, ClientboundContainerSetContentPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundContainerSetData, ClientboundContainerSetDataPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundContainerSetSlot, ClientboundContainerSetSlotPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ClientboundDisconnect, ClientboundDisconnectPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundEntityEvent, ClientboundEntityEventPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundEntityPositionSync, ClientboundEntityPositionSyncPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundForgetLevelChunk, ClientboundForgetLevelChunkPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundGameEvent, ClientboundGameEventPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundHurtAnimation, ClientboundHurtAnimationPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundInitializeBorder, ClientboundInitializeBorderPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ClientboundKeepAlive, ClientboundKeepAlivePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundLevelChunkWithLight, ClientboundLevelChunkWithLightPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundLevelEvent, ClientboundLevelEventPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundLevelParticles, ClientboundLevelParticlesPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundLightUpdate, ClientboundLightUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundLogin, ClientboundLoginPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundMoveEntityPos, ClientboundMoveEntityPacket.Pos.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundMoveEntityPosRot, ClientboundMoveEntityPacket.PosRot.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundMoveEntityRot, ClientboundMoveEntityPacket.Rot.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundOpenScreen, ClientboundOpenScreenPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundPlayerAbilities, ClientboundPlayerAbilitiesPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundPlayerInfoRemove, ClientboundPlayerInfoRemovePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundPlayerInfoUpdate, ClientboundPlayerInfoUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundPlayerPosition, ClientboundPlayerPositionPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundPlayerRotation, ClientboundPlayerRotationPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundRemoveEntities, ClientboundRemoveEntitiesPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundRotateHead, ClientboundRotateHeadPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSectionBlocksUpdate, ClientboundSectionBlocksUpdatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetActionBarText, ClientboundSetActionBarTextPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetBorderCenter, ClientboundSetBorderCenterPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetBorderLerpSize, ClientboundSetBorderLerpSizePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetBorderSize, ClientboundSetBorderSizePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetBorderWarningDelay, ClientboundSetBorderWarningDelayPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetBorderWarningDistance, ClientboundSetBorderWarningDistancePacket.StreamCodec)
            //set_camera 旁观相机切换 玩家跟随相机实体时下发 漏注册会被连接层丢弃客户端视角不切
            .AddPacket(GamePacketTypes.ClientboundSetCamera, ClientboundSetCameraPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetChunkCacheCenter, ClientboundSetChunkCacheCenterPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetChunkCacheRadius, ClientboundSetChunkCacheRadiusPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetCursorItem, ClientboundSetCursorItemPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetDefaultSpawnPosition, ClientboundSetDefaultSpawnPositionPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetEntityData, ClientboundSetEntityDataPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetEntityMotion, ClientboundSetEntityMotionPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetEquipment, ClientboundSetEquipmentPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetExperience, ClientboundSetExperiencePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetHealth, ClientboundSetHealthPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetHeldSlot, ClientboundSetHeldSlotPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetPlayerInventory, ClientboundSetPlayerInventoryPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetSubtitleText, ClientboundSetSubtitleTextPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetTime, ClientboundSetTimePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetTitleText, ClientboundSetTitleTextPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSetTitlesAnimation, ClientboundSetTitlesAnimationPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSoundEntity, ClientboundSoundEntityPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSound, ClientboundSoundPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundStopSound, ClientboundStopSoundPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundSystemChat, ClientboundSystemChatPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundTakeItemEntity, ClientboundTakeItemEntityPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundTeleportEntity, ClientboundTeleportEntityPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundTickingState, ClientboundTickingStatePacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundTickingStep, ClientboundTickingStepPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ClientboundTransfer, ClientboundTransferPacket.StreamCodec)
            //药水效果同步包 未注册会被连接层丢弃 客户端看不到效果图标
            .AddPacket(GamePacketTypes.ClientboundRemoveMobEffect, ClientboundRemoveMobEffectPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundUpdateMobEffect, ClientboundUpdateMobEffectPacket.StreamCodec)
            //pong_response 是 ping_request 的回复 原版 play 协议 id 62 客户端据此算延迟
            .AddPacketCommon(GamePacketTypes.ClientboundPongResponse, ClientboundPongResponsePacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ClientboundUpdateTags, ClientboundUpdateTagsPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientGamePacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
