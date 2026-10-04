using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSoundPacket 声音包对应原版 ClientboundSoundPacket
//声音写内联 holder 坐标按 8 倍精度 int 编解码
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
