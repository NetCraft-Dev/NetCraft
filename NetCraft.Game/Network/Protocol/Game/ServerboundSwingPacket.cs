namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSwingPacket swing packet, maps to vanilla ServerboundSwingPacket
//Field: Hand(InteractionHand)
public sealed record ServerboundSwingPacket(InteractionHand Hand) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSwingPacket> StreamCodec { get; } = new SwingCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSwing;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAnimate(this);

    private sealed class SwingCodec : StreamCodec<FriendlyByteBuf, ServerboundSwingPacket>
    {
        //Vanilla writeEnum(hand), i.e. a VarInt enum ordinal
        public ServerboundSwingPacket Decode(FriendlyByteBuf buf)
            => new((InteractionHand)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundSwingPacket value)
            => buf.WriteVarInt((int)value.Hand);
    }
}

//InteractionHand interaction hand enum, maps to vanilla net.minecraft.world.InteractionHand
//Ordinals must match vanilla; vanilla writes a VarInt enum ordinal
public enum InteractionHand
{
    MainHand,
    OffHand
}
