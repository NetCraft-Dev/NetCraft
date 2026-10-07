namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPaddleBoatPacket paddle boat packet, maps to vanilla ServerboundPaddleBoatPacket
//Fields: Left(boolean), Right(boolean)
public sealed record ServerboundPaddleBoatPacket(bool Left, bool Right) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPaddleBoatPacket> StreamCodec { get; } = new PaddleBoatCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPaddleBoat;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePaddleBoat(this);

    private sealed class PaddleBoatCodec : StreamCodec<FriendlyByteBuf, ServerboundPaddleBoatPacket>
    {
        //While rowing, reports the left/right paddle state, with Left first
        public ServerboundPaddleBoatPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBoolean(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundPaddleBoatPacket value)
        {
            buf.WriteBoolean(value.Left);
            buf.WriteBoolean(value.Right);
        }
    }
}
