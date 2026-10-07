using System.Collections.Generic;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundDebugSubscriptionRequestPacket debug subscription request packet, maps to vanilla ServerboundDebugSubscriptionRequestPacket
//Field: Subscriptions(List<int> set of subscription registry ids)
public sealed record ServerboundDebugSubscriptionRequestPacket(List<int> Subscriptions) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundDebugSubscriptionRequestPacket> StreamCodec { get; } = new DebugSubscriptionRequestCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundDebugSubscriptionRequest;

    public void Handle(ServerGamePacketListener handler) => handler.HandleDebugSubscriptionRequest(this);

    private sealed class DebugSubscriptionRequestCodec : StreamCodec<FriendlyByteBuf, ServerboundDebugSubscriptionRequestPacket>
    {
        //Sent when toggling subscriptions in the F3 debug screen; vanilla encodes it as a collection: varint count plus a registry id per entry
        public ServerboundDebugSubscriptionRequestPacket Decode(FriendlyByteBuf buf)
        {
            var count = buf.ReadVarInt();
            var subscriptions = new List<int>(count);
            for (var i = 0; i < count; i++) subscriptions.Add(buf.ReadVarInt());
            return new(subscriptions);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundDebugSubscriptionRequestPacket value)
        {
            buf.WriteVarInt(value.Subscriptions.Count);
            foreach (var subscription in value.Subscriptions) buf.WriteVarInt(subscription);
        }
    }
}
