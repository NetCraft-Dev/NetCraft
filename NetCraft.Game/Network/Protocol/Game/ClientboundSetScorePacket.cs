namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetScorePacket 分数设置包对应原版 ClientboundSetScorePacket
//字段 Owner(String) ObjectiveName(String) Score(int) Display(Optional<Component>) NumberFormat(Optional<NumberFormat>)
public sealed record ClientboundSetScorePacket(string Owner, string ObjectiveName, int Score, object Display, object NumberFormat) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetScorePacket> StreamCodec { get; } = new SetScoreCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetScore;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetScore(this);

    private sealed class SetScoreCodec : StreamCodec<FriendlyByteBuf, ClientboundSetScorePacket>
    {
        public ClientboundSetScorePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetScorePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
