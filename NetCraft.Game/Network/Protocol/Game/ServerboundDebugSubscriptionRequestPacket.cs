using System.Collections.Generic;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundDebugSubscriptionRequestPacket 数据包对应原版 ServerboundDebugSubscriptionRequestPacket
//字段 Subscriptions(List<int> 订阅项注册表 id 集合)
public sealed record ServerboundDebugSubscriptionRequestPacket(List<int> Subscriptions) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundDebugSubscriptionRequestPacket> StreamCodec { get; } = new DebugSubscriptionRequestCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundDebugSubscriptionRequest;

    public void Handle(ServerGamePacketListener handler) => handler.HandleDebugSubscriptionRequest(this);

    private sealed class DebugSubscriptionRequestCodec : StreamCodec<FriendlyByteBuf, ServerboundDebugSubscriptionRequestPacket>
    {
        //F3 调试界面切换订阅时发送 原版按 collection 编码为 varint 个数加每项注册表 id
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
