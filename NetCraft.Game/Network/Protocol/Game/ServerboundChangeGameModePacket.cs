using NetCraft.Game.World.Level;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChangeGameModePacket change game mode packet, maps to vanilla ServerboundChangeGameModePacket
//Field: Mode(GameType); sent from the client when switching game mode with F3+F4
public sealed record ServerboundChangeGameModePacket(GameType Mode) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChangeGameModePacket> StreamCodec { get; } = new ChangeGameModeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChangeGameMode;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChangeGameMode(this);

    private sealed class ChangeGameModeCodec : StreamCodec<FriendlyByteBuf, ServerboundChangeGameModePacket>
    {
        public ServerboundChangeGameModePacket Decode(FriendlyByteBuf buf)
            => new(GameType.ById(buf.ReadVarInt()) ?? throw new InvalidOperationException("Unknown game mode"));

        public void Encode(FriendlyByteBuf buf, ServerboundChangeGameModePacket value)
            => buf.WriteVarInt(value.Mode.Id);
    }
}
