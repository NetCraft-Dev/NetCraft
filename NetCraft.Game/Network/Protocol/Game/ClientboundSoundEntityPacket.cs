using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSoundEntityPacket sound entity packet, maps to vanilla ClientboundSoundEntityPacket
//The sound is written as an inline holder; the entity id uses VarInt
public sealed record ClientboundSoundEntityPacket(SoundEvent Sound, SoundSource Source, int Id, float Volume, float Pitch, long Seed) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSoundEntityPacket> StreamCodec { get; } = new SoundEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSoundEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSoundEntityEvent(this);

    private sealed class SoundEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundSoundEntityPacket>
    {
        public ClientboundSoundEntityPacket Decode(FriendlyByteBuf buf)
        {
            var sound = ReadSound(buf);
            return new(
                sound,
                (SoundSource)buf.ReadVarInt(),
                buf.ReadVarInt(),
                buf.ReadFloat(),
                buf.ReadFloat(),
                buf.ReadLong());
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSoundEntityPacket value)
        {
            WriteSound(buf, value.Sound);
            buf.WriteVarInt((int)value.Source);
            buf.WriteVarInt(value.Id);
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
