using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSoundEntityPacket 实体声音包对应原版 ClientboundSoundEntityPacket
//声音写内联 holder 实体 id 用 VarInt
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

        //ReadSound 读声音 holder 本项目 sound_event 注册表为空只能识别内联形式
        private static SoundEvent ReadSound(FriendlyByteBuf buf)
        {
            if (buf.ReadVarInt() != 0)
                //非 0 是注册表项引用 空注册表无从解析原版 id
                throw new NotSupportedException("sound_event 注册表项引用暂不支持");
            var location = buf.ReadIdentifier();
            float? fixedRange = buf.ReadBoolean() ? buf.ReadFloat() : null;
            return new SoundEvent(location, fixedRange);
        }

        //WriteSound 写声音 holder 一律写内联
        private static void WriteSound(FriendlyByteBuf buf, SoundEvent sound)
        {
            buf.WriteVarInt(0);
            buf.WriteIdentifier(sound.Location);
            buf.WriteBoolean(sound.FixedRange.HasValue);
            if (sound.FixedRange.HasValue) buf.WriteFloat(sound.FixedRange.Value);
        }
    }
}
