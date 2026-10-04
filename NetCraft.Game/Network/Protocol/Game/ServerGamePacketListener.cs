using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerGamePacketListener 服务端 Play 阶段监听器对应原版 net.minecraft.network.protocol.game.ServerGamePacketListener
//继承 ServerCommonPacketListener 和 ServerPingPacketListener 处理所有 Serverbound game 包共 58 个
public interface ServerGamePacketListener : ServerCommonPacketListener, ServerPingPacketListener
{
    void HandleAnimate(ServerboundSwingPacket packet);
    void HandleChat(ServerboundChatPacket packet);
    void HandleChatCommand(ServerboundChatCommandPacket packet);
    void HandleSignedChatCommand(ServerboundChatCommandSignedPacket packet);
    void HandleChatAck(ServerboundChatAckPacket packet);
    void HandleClientCommand(ServerboundClientCommandPacket packet);
    void HandleContainerButtonClick(ServerboundContainerButtonClickPacket packet);
    void HandleContainerClick(ServerboundContainerClickPacket packet);
    void HandlePlaceRecipe(ServerboundPlaceRecipePacket packet);
    void HandleContainerClose(ServerboundContainerClosePacket packet);
    void HandleAttack(ServerboundAttackPacket packet);
    void HandleInteract(ServerboundInteractPacket packet);
    void HandleSpectatorAction(ServerboundSpectatorActionPacket packet);
    void HandleMovePlayer(ServerboundMovePlayerPacket packet);
    void HandlePlayerAbilities(ServerboundPlayerAbilitiesPacket packet);
    void HandlePlayerAction(ServerboundPlayerActionPacket packet);
    void HandlePlayerCommand(ServerboundPlayerCommandPacket packet);
    void HandlePlayerInput(ServerboundPlayerInputPacket packet);
    void HandleSetCarriedItem(ServerboundSetCarriedItemPacket packet);
    void HandleSetCreativeModeSlot(ServerboundSetCreativeModeSlotPacket packet);
    void HandleSignUpdate(ServerboundSignUpdatePacket packet);
    void HandleUseItemOn(ServerboundUseItemOnPacket packet);
    void HandleUseItem(ServerboundUseItemPacket packet);
    void HandleTeleportToEntityPacket(ServerboundTeleportToEntityPacket packet);
    void HandlePaddleBoat(ServerboundPaddleBoatPacket packet);
    void HandleMoveVehicle(ServerboundMoveVehiclePacket packet);
    void HandleAcceptTeleportPacket(ServerboundAcceptTeleportationPacket packet);
    void HandleAcceptPlayerLoad(ServerboundPlayerLoadedPacket packet);
    void HandleRecipeBookSeenRecipePacket(ServerboundRecipeBookSeenRecipePacket packet);
    void HandleBundleItemSelectedPacket(ServerboundSelectBundleItemPacket packet);
    void HandleRecipeBookChangeSettingsPacket(ServerboundRecipeBookChangeSettingsPacket packet);
    void HandleSeenAdvancements(ServerboundSeenAdvancementsPacket packet);
    void HandleCustomCommandSuggestions(ServerboundCommandSuggestionPacket packet);
    void HandleSetCommandBlock(ServerboundSetCommandBlockPacket packet);
    void HandleSetCommandMinecart(ServerboundSetCommandMinecartPacket packet);
    void HandlePickItemFromBlock(ServerboundPickItemFromBlockPacket packet);
    void HandlePickItemFromEntity(ServerboundPickItemFromEntityPacket packet);
    void HandleRenameItem(ServerboundRenameItemPacket packet);
    void HandleSetBeaconPacket(ServerboundSetBeaconPacket packet);
    void HandleSetGameRule(ServerboundSetGameRulePacket packet);
    void HandleSetStructureBlock(ServerboundSetStructureBlockPacket packet);
    void HandleSetTestBlock(ServerboundSetTestBlockPacket packet);
    void HandleTestInstanceBlockAction(ServerboundTestInstanceBlockActionPacket packet);
    void HandleSelectTrade(ServerboundSelectTradePacket packet);
    void HandleEditBook(ServerboundEditBookPacket packet);
    void HandleEntityTagQuery(ServerboundEntityTagQueryPacket packet);
    void HandleContainerSlotStateChanged(ServerboundContainerSlotStateChangedPacket packet);
    void HandleBlockEntityTagQuery(ServerboundBlockEntityTagQueryPacket packet);
    void HandleSetJigsawBlock(ServerboundSetJigsawBlockPacket packet);
    void HandleJigsawGenerate(ServerboundJigsawGeneratePacket packet);
    void HandleChangeDifficulty(ServerboundChangeDifficultyPacket packet);
    void HandleChangeGameMode(ServerboundChangeGameModePacket packet);
    void HandleLockDifficulty(ServerboundLockDifficultyPacket packet);
    void HandleChatSessionUpdate(ServerboundChatSessionUpdatePacket packet);
    void HandleConfigurationAcknowledged(ServerboundConfigurationAcknowledgedPacket packet);
    void HandleChunkBatchReceived(ServerboundChunkBatchReceivedPacket packet);
    void HandleDebugSubscriptionRequest(ServerboundDebugSubscriptionRequestPacket packet);
    void HandleClientTickEnd(ServerboundClientTickEndPacket packet);
}
