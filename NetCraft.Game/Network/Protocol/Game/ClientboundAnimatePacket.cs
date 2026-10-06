namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundAnimatePacket entity animation packet, maps to vanilla ClientboundAnimatePacket
//Fields: Id(int), Action(int)
public sealed record ClientboundAnimatePacket(int Id, int Action) : Packet<ClientGamePacketListener>
{
    //Action constants align with vanilla ClientboundAnimatePacket
    public const int SwingMainHand = 0;
    public const int SwingOffHand = 3;

    public static StreamCodec<FriendlyByteBuf, ClientboundAnimatePacket> StreamCodec { get; } = new AnimateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundAnimate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleAnimate(this);

    private sealed class AnimateCodec : StreamCodec<FriendlyByteBuf, ClientboundAnimatePacket>
    {
        //Vanilla order: VAR_INT entity id, unsigned byte action; see SwingMainHand etc. for action constants
        public ClientboundAnimatePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadByte());

        public void Encode(FriendlyByteBuf buf, ClientboundAnimatePacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteByte((byte)value.Action);
        }
    }
}
