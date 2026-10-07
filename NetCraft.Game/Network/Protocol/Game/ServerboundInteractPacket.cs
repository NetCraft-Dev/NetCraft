using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundInteractPacket interact packet, maps to vanilla ServerboundInteractPacket
//Fields: EntityId(int), Hand(InteractionHand), Location(low-precision Vec3), UsingSecondaryAction(boolean)
//Location is the hit point of the player's line of sight on the entity hitbox; the server does not use the current position but the field must still be read as-is
public sealed record ServerboundInteractPacket(
    int EntityId, InteractionHand Hand, Vec3 Location, bool UsingSecondaryAction) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundInteractPacket> StreamCodec { get; } = new InteractCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundInteract;

    public void Handle(ServerGamePacketListener handler) => handler.HandleInteract(this);

    private sealed class InteractCodec : StreamCodec<FriendlyByteBuf, ServerboundInteractPacket>
    {
        //Vanilla order: VAR_INT entityId, writeEnum(hand), LpVec3 location, BOOL usingSecondaryAction
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
