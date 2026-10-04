namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSwingPacket 挥手包对应原版 ServerboundSwingPacket
//字段 Hand(InteractionHand)
public sealed record ServerboundSwingPacket(InteractionHand Hand) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSwingPacket> StreamCodec { get; } = new SwingCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSwing;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAnimate(this);

    private sealed class SwingCodec : StreamCodec<FriendlyByteBuf, ServerboundSwingPacket>
    {
        //原版 writeEnum(hand) 即 VarInt 枚举序号
        public ServerboundSwingPacket Decode(FriendlyByteBuf buf)
            => new((InteractionHand)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundSwingPacket value)
            => buf.WriteVarInt((int)value.Hand);
    }
}

//InteractionHand 交互手枚举对应原版 net.minecraft.world.InteractionHand
//序号必须与原版一致 原版按枚举序号写 VarInt
public enum InteractionHand
{
    MainHand,
    OffHand
}
