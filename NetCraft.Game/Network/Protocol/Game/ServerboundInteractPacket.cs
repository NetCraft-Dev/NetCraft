using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundInteractPacket 交互包对应原版 ServerboundInteractPacket
//字段 EntityId(int) Hand(InteractionHand) Location(Vec3 低精度) UsingSecondaryAction(boolean)
//Location 是玩家视线与实体碰撞箱的命中点 服务端用不上当前位置但字段必须原样读掉
public sealed record ServerboundInteractPacket(
    int EntityId, InteractionHand Hand, Vec3 Location, bool UsingSecondaryAction) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundInteractPacket> StreamCodec { get; } = new InteractCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundInteract;

    public void Handle(ServerGamePacketListener handler) => handler.HandleInteract(this);

    private sealed class InteractCodec : StreamCodec<FriendlyByteBuf, ServerboundInteractPacket>
    {
        //原版顺序 VAR_INT entityId writeEnum(hand) LpVec3 location BOOL usingSecondaryAction
        public ServerboundInteractPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), (InteractionHand)buf.ReadVarInt(), LpVec3.Read(buf), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundInteractPacket value)
        {
            buf.WriteVarInt(value.EntityId);
            buf.WriteVarInt((int)value.Hand);
            LpVec3.Write(buf, value.Location);
            buf.WriteBoolean(value.UsingSecondaryAction);
        }
    }
}
