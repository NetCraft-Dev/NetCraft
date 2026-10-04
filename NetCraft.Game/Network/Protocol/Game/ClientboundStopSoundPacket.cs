using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundStopSoundPacket 停止声音包对应原版 ClientboundStopSoundPacket
//字段 Name(音效标识) Source(音效分类) 都可为空
public sealed record ClientboundStopSoundPacket(Identifier? Name, SoundSource? Source) : Packet<ClientGamePacketListener>
{
    //StopSourceFlag 位掩码 bit0 表示带 Source
    private const byte StopSourceFlag = 1;
    //StopNameFlag 位掩码 bit1 表示带 Name
    private const byte StopNameFlag = 2;

    public static StreamCodec<FriendlyByteBuf, ClientboundStopSoundPacket> StreamCodec { get; } = new StopSoundCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundStopSound;

    public void Handle(ClientGamePacketListener handler) => handler.HandleStopSoundEvent(this);

    private sealed class StopSoundCodec : StreamCodec<FriendlyByteBuf, ClientboundStopSoundPacket>
    {
        public ClientboundStopSoundPacket Decode(FriendlyByteBuf buf)
        {
            var flags = buf.ReadByte();
            SoundSource? source = (flags & StopSourceFlag) != 0 ? (SoundSource)buf.ReadVarInt() : null;
            Identifier? name = (flags & StopNameFlag) != 0 ? buf.ReadIdentifier() : null;
            return new(name, source);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundStopSoundPacket value)
        {
            byte flags = 0;
            if (value.Source.HasValue) flags |= StopSourceFlag;
            if (value.Name.HasValue) flags |= StopNameFlag;
            buf.WriteByte(flags);
            if (value.Source.HasValue) buf.WriteVarInt((int)value.Source.Value);
            if (value.Name.HasValue) buf.WriteIdentifier(value.Name.Value);
        }
    }
}
