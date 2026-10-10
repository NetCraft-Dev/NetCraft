using NetCraft.Network.Chat;

namespace NetCraft.Network.Protocol.Common;

//ClientboundDisconnectPacket client disconnect packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundDisconnectPacket
//reason is a component and uses the component stream codec as in vanilla; writing it as a string would make the client misinterpret the length prefix as an NBT tag id and fail to parse
//IsTerminal true means the connection closes after disconnecting
//Component uses a fully qualified name; under the outer namespace NetCraft.Network.* the simple name would be captured by the NetCraft.Game.World.Items.Component namespace
public sealed record ClientboundDisconnectPacket(NetCraft.Network.Chat.Component Reason) : Packet<ClientCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDisconnectPacket> StreamCodec { get; } = new DisconnectCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundDisconnect;

    //IsTerminal the connection closes after the disconnect packet
    public bool IsTerminal => true;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleDisconnect(this);

    private sealed class DisconnectCodec : StreamCodec<FriendlyByteBuf, ClientboundDisconnectPacket>
    {
        public ClientboundDisconnectPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundDisconnectPacket value)
            => ComponentSerialization.StreamCodec.Encode(buf, value.Reason);
    }
}
