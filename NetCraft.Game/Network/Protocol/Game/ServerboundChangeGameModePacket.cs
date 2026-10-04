using NetCraft.Game.World.Level;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChangeGameModePacket 数据包对应原版 ServerboundChangeGameModePacket
//字段 Mode(GameType) 客户端 F3+F4 切换游戏模式时上行
public sealed record ServerboundChangeGameModePacket(GameType Mode) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChangeGameModePacket> StreamCodec { get; } = new ChangeGameModeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChangeGameMode;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChangeGameMode(this);

    private sealed class ChangeGameModeCodec : StreamCodec<FriendlyByteBuf, ServerboundChangeGameModePacket>
    {
        public ServerboundChangeGameModePacket Decode(FriendlyByteBuf buf)
            => new(GameType.ById(buf.ReadVarInt()) ?? throw new InvalidOperationException("未知游戏模式"));

        public void Encode(FriendlyByteBuf buf, ServerboundChangeGameModePacket value)
            => buf.WriteVarInt(value.Mode.Id);
    }
}
