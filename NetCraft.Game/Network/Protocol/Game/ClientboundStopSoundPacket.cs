using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundStopSoundPacket stop sound packet, maps to vanilla ClientboundStopSoundPacket
//Fields: Name (sound identifier), Source (sound category); both may be empty
public sealed record ClientboundStopSoundPacket(Identifier? Name, SoundSource? Source) : Packet<ClientGamePacketListener>
{
    //StopSourceFlag bitmask bit0 means a Source is present
    private const byte StopSourceFlag = 1;
    //StopNameFlag bitmask bit1 means a Name is present
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
