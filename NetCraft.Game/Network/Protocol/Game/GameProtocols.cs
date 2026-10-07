using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Game.Network.Protocol.Game;

//GameProtocols play protocol registration
//Maps to vanilla net.minecraft.network.protocol.game.GameProtocols
//IDs are strictly aligned with the vanilla registration order for 26.2 by GamePacketTypes, so real clients can decode
//clientbound registers the packets required for a player to enter the world + common keepalive/disconnect sent by the server
//serverbound registers the packets a real client actively sends after entering play (move/chunk ack/player_loaded etc.)
//An unregistered packet decodes to "unknown packet" and is dropped without affecting the connection
public static class GameProtocols
{
    //ServerboundTemplate SERVERBOUND play protocol template
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
            //ping_request is sent periodically by the client's PingDebugMonitor and the server replies with pong_response; vanilla play protocol id 38
            .AddPacketCommon(GamePacketTypes.ServerboundPingRequest, ServerboundPingRequestPacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ServerboundPong, ServerboundPongPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSetCarriedItem, ServerboundSetCarriedItemPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundSetCreativeModeSlot, ServerboundSetCreativeModeSlotPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ServerboundRecipeBookChangeSettings, ServerboundRecipeBookChangeSettingsPacket.StreamCodec)
            //S4.186 adds common interaction serverbound packets; a vanilla client sends them when opening screens, and not registering them spams unknown-packet warnings
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

    //Serverbound the bound SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerGamePacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND play protocol template
    //S3 adds the chunk send chain, registering beyond Login/PlayerInfo/SystemChat the full SendJoinPackets sequence and chunk packets
    //S4 aligns the 26.2 packet IDs and adds common keepalive/disconnect so real clients can decode
    //C3 adds container and equipment packet registration
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
            //set_camera spectator camera switch, sent when the player follows a camera entity; if not registered the connection layer drops it and the client camera does not switch
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
            //Mob effect sync packets; if not registered the connection layer drops them and the client shows no effect icons
            .AddPacket(GamePacketTypes.ClientboundRemoveMobEffect, ClientboundRemoveMobEffectPacket.StreamCodec)
            .AddPacket(GamePacketTypes.ClientboundUpdateMobEffect, ClientboundUpdateMobEffectPacket.StreamCodec)
            //pong_response is the reply to ping_request; vanilla play protocol id 62, used by the client to compute latency
            .AddPacketCommon(GamePacketTypes.ClientboundPongResponse, ClientboundPongResponsePacket.StreamCodec)
            .AddPacketCommon(CommonPacketTypes.ClientboundUpdateTags, ClientboundUpdateTagsPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound the bound CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientGamePacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
