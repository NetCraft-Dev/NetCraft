using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSoundPacket sound packet, maps to vanilla ClientboundSoundPacket
//The sound is written as an inline holder; coordinates are encoded as ints at 8x precision
public sealed record ClientboundSoundPacket(SoundEvent Sound, SoundSource Source, double X, double Y, double Z, float Volume, float Pitch, long Seed) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSoundPacket> StreamCodec { get; } = new SoundCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSound;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSoundEvent(this);

    private sealed class SoundCodec : StreamCodec<FriendlyByteBuf, ClientboundSoundPacket>
    {
        public ClientboundSoundPacket Decode(FriendlyByteBuf buf)
        {
            var sound = ReadSound(buf);
            return new(
                sound,
                (SoundSource)buf.ReadVarInt(),
                buf.ReadInt() / 8.0,
                buf.ReadInt() / 8.0,
                buf.ReadInt() / 8.0,
                buf.ReadFloat(),
                buf.ReadFloat(),
                buf.ReadLong());
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSoundPacket value)
        {
            WriteSound(buf, value.Sound);
            buf.WriteVarInt((int)value.Source);
            buf.WriteInt((int)Math.Floor(value.X * 8.0));
            buf.WriteInt((int)Math.Floor(value.Y * 8.0));
            buf.WriteInt((int)Math.Floor(value.Z * 8.0));
            buf.WriteFloat(value.Volume);
            buf.WriteFloat(value.Pitch);
            buf.WriteLong(value.Seed);
        }

        //ReadSound reads the sound holder; this project's sound_event registry is empty, so only the inline form can be recognized
        private static SoundEvent ReadSound(FriendlyByteBuf buf)
        {
            if (buf.ReadVarInt() != 0)
                //A non-zero value is a registry entry reference; an empty registry cannot resolve the vanilla id
                throw new NotSupportedException("sound_event registry entry references are not supported yet");
            var location = buf.ReadIdentifier();
            float? fixedRange = buf.ReadBoolean() ? buf.ReadFloat() : null;
            return new SoundEvent(location, fixedRange);
        }

        //WriteSound writes the sound holder, always in the inline form
        private static void WriteSound(FriendlyByteBuf buf, SoundEvent sound)
        {
            buf.WriteVarInt(0);
            buf.WriteIdentifier(sound.Location);
            buf.WriteBoolean(sound.FixedRange.HasValue);
            if (sound.FixedRange.HasValue) buf.WriteFloat(sound.FixedRange.Value);
        }
    }
}
