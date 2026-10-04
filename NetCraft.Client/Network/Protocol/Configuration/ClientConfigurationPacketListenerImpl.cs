using NetCraft.Network.Protocol.Common;
using NetCraft.Network.Protocol.Cookie;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Configuration;

//ClientConfigurationPacketListenerImpl 客户端 configuration 监听器实现
//对应原版 ClientConfigurationPacketListenerImpl 简化版
//服务器 Configuration 阶段不下发 registry/features 直接等客户端 FinishConfiguration
//TransitionToPlay 发 FinishConfiguration 切 Play 协议挂 gameListener
public sealed class ClientConfigurationPacketListenerImpl : ClientConfigurationPacketListener
{
    private readonly Connection _connection;
    private readonly ClientGamePacketListenerImpl _gameListener;

    public ClientConfigurationPacketListenerImpl(Connection connection, ClientGamePacketListenerImpl gameListener)
    {
        _connection = connection;
        _gameListener = gameListener;
    }

    //TransitionToPlay 发 FinishConfiguration 并切 Play 协议
    //调用前提 inbound/outbound 已是 Configuration 协议
    public void TransitionToPlay()
    {
        Log.Info("Configuration finished, sending FinishConfiguration and switching to Play phase");
        _connection.Send(ServerboundFinishConfigurationPacket.Instance);
        _connection.SetupInboundProtocol(GameProtocols.Clientbound, _gameListener);
        _connection.SetupOutboundProtocol(GameProtocols.Serverbound);
    }

    //HandleConfigurationFinished 服务器主动下发完成包对齐原版流程收包即切 Play
    //当前服务端不下发此包由 TransitionToPlay 主动推进保留对齐入口
    public void HandleConfigurationFinished(ClientboundFinishConfigurationPacket packet)
        => TransitionToPlay();

    public void HandleRegistryData(ClientboundRegistryDataPacket packet) { }
    public void HandleEnabledFeatures(ClientboundUpdateEnabledFeaturesPacket packet) { }

    //HandleSelectKnownPacks 回传客户端已加载的资源包 与服务端请求的 minecraft:core:26.2 一致
    //回复后服务端才发 registry_data 与 finish_configuration 不回会停在配置阶段
    public void HandleSelectKnownPacks(ClientboundSelectKnownPacks packet)
    {
        var core = new KnownPack("minecraft", "core", "26.2");
        Log.Debug($"Configuration phase replying SelectKnownPacks {core}");
        _connection.Send(new ServerboundSelectKnownPacks(new List<KnownPack> { core }));
    }
    public void HandleResetChat(ClientboundResetChatPacket packet) { }
    public void HandleCodeOfConduct(ClientboundCodeOfConductPacket packet) { }

    //以下继承自 ClientCommonPacketListener 当前服务器不下发空实现
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

    //继承自 ClientCookiePacketListener
    public void HandleCookieRequest(ClientboundCookieRequestPacket packet) { }

    public void OnDisconnect(string reason)
        => Log.Info($"configuration phase disconnect reason={reason}");
}
