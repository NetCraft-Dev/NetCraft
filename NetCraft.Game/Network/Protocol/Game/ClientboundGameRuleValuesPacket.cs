namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameRuleValuesPacket 游戏规则值包对应原版 ClientboundGameRuleValuesPacket
//字段 values Map ResourceKey GameRule String 业务类型占位
public sealed record ClientboundGameRuleValuesPacket(object Values) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundGameRuleValuesPacket> StreamCodec { get; } = new GameRuleValuesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundGameRuleValues;

    public void Handle(ClientGamePacketListener handler) => handler.HandleGameRuleValues(this);

    private sealed class GameRuleValuesCodec : StreamCodec<FriendlyByteBuf, ClientboundGameRuleValuesPacket>
    {
        public ClientboundGameRuleValuesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Map ResourceKey GameRule String 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundGameRuleValuesPacket value)
            => throw new NotImplementedException("Map ResourceKey GameRule String 业务类型待实现");
    }
}
