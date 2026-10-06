namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameRuleValuesPacket game rule values packet, maps to vanilla ClientboundGameRuleValuesPacket
//Fields: values Map ResourceKey GameRule String business type placeholder
public sealed record ClientboundGameRuleValuesPacket(object Values) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundGameRuleValuesPacket> StreamCodec { get; } = new GameRuleValuesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundGameRuleValues;

    public void Handle(ClientGamePacketListener handler) => handler.HandleGameRuleValues(this);

    private sealed class GameRuleValuesCodec : StreamCodec<FriendlyByteBuf, ClientboundGameRuleValuesPacket>
    {
        public ClientboundGameRuleValuesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Map ResourceKey GameRule String business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundGameRuleValuesPacket value)
            => throw new NotImplementedException("Map ResourceKey GameRule String business type not yet implemented");
    }
}
