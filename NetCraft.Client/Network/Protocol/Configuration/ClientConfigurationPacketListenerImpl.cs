using NetCraft.Network.Protocol.Common;
using NetCraft.Network.Protocol.Cookie;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Configuration;

//ClientConfigurationPacketListenerImpl client configuration listener implementation
//Simplified version of vanilla ClientConfigurationPacketListenerImpl
//In the Configuration phase the server does not send registry/features; it waits directly for the client's FinishConfiguration
//TransitionToPlay sends FinishConfiguration, switches to the Play protocol, and attaches gameListener
public sealed class ClientConfigurationPacketListenerImpl : ClientConfigurationPacketListener
{
    private readonly Connection _connection;
    private readonly ClientGamePacketListenerImpl _gameListener;

    public ClientConfigurationPacketListenerImpl(Connection connection, ClientGamePacketListenerImpl gameListener)
    {
        _connection = connection;
        _gameListener = gameListener;
    }

    //TransitionToPlay sends FinishConfiguration and switches to the Play protocol
    //Precondition: inbound/outbound are already on the Configuration protocol
    public void TransitionToPlay()
    {
        Log.Info("Configuration finished, sending FinishConfiguration and switching to Play phase");
        _connection.Send(ServerboundFinishConfigurationPacket.Instance);
        _connection.SetupInboundProtocol(GameProtocols.Clientbound, _gameListener);
        _connection.SetupOutboundProtocol(GameProtocols.Serverbound);
    }

    //HandleConfigurationFinished the server proactively sends the finish packet; aligns with vanilla flow, switching to Play on receipt
    //Currently the server does not send this packet; TransitionToPlay advances proactively, keeping this entry for alignment
    public void HandleConfigurationFinished(ClientboundFinishConfigurationPacket packet)
        => TransitionToPlay();

    public void HandleRegistryData(ClientboundRegistryDataPacket packet) { }
    public void HandleEnabledFeatures(ClientboundUpdateEnabledFeaturesPacket packet) { }

    //HandleSelectKnownPacks replies with the resource packs the client has loaded, matching the server's requested minecraft:core:26.2
    //Only after replying does the server send registry_data and finish_configuration; without a reply it stalls in the configuration phase
    public void HandleSelectKnownPacks(ClientboundSelectKnownPacks packet)
    {
        var core = new KnownPack("minecraft", "core", "26.2");
        Log.Debug($"Configuration phase replying SelectKnownPacks {core}");
        _connection.Send(new ServerboundSelectKnownPacks(new List<KnownPack> { core }));
    }
    public void HandleResetChat(ClientboundResetChatPacket packet) { }
    public void HandleCodeOfConduct(ClientboundCodeOfConductPacket packet) { }

    //The following are inherited from ClientCommonPacketListener; the current server does not send them, empty implementation
    public void HandleKeepAlive(ClientboundKeepAlivePacket packet) { }
    public void HandlePing(ClientboundPingPacket packet) { }
    public void HandleCustomPayload(ClientboundCustomPayloadPacket packet) { }
    public void HandleDisconnect(ClientboundDisconnectPacket packet)
        => Log.Warning($"configuration phase disconnected {packet.Reason}");
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
        => Log.Info($"configuration phase disconnect reason={reason}");
}
