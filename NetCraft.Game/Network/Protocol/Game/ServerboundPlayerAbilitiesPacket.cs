namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerAbilitiesPacket 玩家能力包对应原版 ServerboundPlayerAbilitiesPacket
//客户端只在切换飞行状态时发送 网络格式为单字节位掩码 bit1=flying
public sealed record ServerboundPlayerAbilitiesPacket(bool IsFlying) : Packet<ServerGamePacketListener>
{
    private const byte FlagFlying = 2;

    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerAbilitiesPacket> StreamCodec { get; } = new PlayerAbilitiesCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerAbilities;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerAbilities(this);

    private sealed class PlayerAbilitiesCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerAbilitiesPacket>
    {
        public ServerboundPlayerAbilitiesPacket Decode(FriendlyByteBuf buf)
            => new((buf.ReadByte() & FlagFlying) != 0);

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerAbilitiesPacket value)
            => buf.WriteByte((byte)(value.IsFlying ? FlagFlying : 0));
    }
}
