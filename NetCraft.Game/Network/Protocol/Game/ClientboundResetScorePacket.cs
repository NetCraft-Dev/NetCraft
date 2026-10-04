namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundResetScorePacket 重置分数包对应原版 ClientboundResetScorePacket
//字段 Owner(String) ObjectiveName(String)
public sealed record ClientboundResetScorePacket(string Owner, string ObjectiveName) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundResetScorePacket> StreamCodec { get; } = new ResetScoreCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundResetScore;

    public void Handle(ClientGamePacketListener handler) => handler.HandleResetScore(this);

    private sealed class ResetScoreCodec : StreamCodec<FriendlyByteBuf, ClientboundResetScorePacket>
    {
        public ClientboundResetScorePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundResetScorePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
