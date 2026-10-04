namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAnimatePacket 实体动画包对应原版 ClientboundAnimatePacket
//字段 Id(int) Action(int)
public sealed record ClientboundAnimatePacket(int Id, int Action) : Packet<ClientGamePacketListener>
{
    //动作常量对齐原版 ClientboundAnimatePacket
    public const int SwingMainHand = 0;
    public const int SwingOffHand = 3;

    public static StreamCodec<FriendlyByteBuf, ClientboundAnimatePacket> StreamCodec { get; } = new AnimateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAnimate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAnimate(this);

    private sealed class AnimateCodec : StreamCodec<FriendlyByteBuf, ClientboundAnimatePacket>
    {
        //原版顺序 VAR_INT 实体id 无符号 byte 动作 动作常量见 SwingMainHand 等
        public ClientboundAnimatePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadByte());

        public void Encode(FriendlyByteBuf buf, ClientboundAnimatePacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteByte((byte)value.Action);
        }
    }
}
