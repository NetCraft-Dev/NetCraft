namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetExperiencePacket 经验值包对应原版 ClientboundSetExperiencePacket
//字段 ExperienceProgress(float) TotalExperience(int) ExperienceLevel(int)
public sealed record ClientboundSetExperiencePacket(float ExperienceProgress, int TotalExperience, int ExperienceLevel) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetExperiencePacket> StreamCodec { get; } = new SetExperienceCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetExperience;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetExperience(this);

    private sealed class SetExperienceCodec : StreamCodec<FriendlyByteBuf, ClientboundSetExperiencePacket>
    {
        //S4 原版顺序 progress(float) level(varint) total(varint) 非 total 在前
        public ClientboundSetExperiencePacket Decode(FriendlyByteBuf buf)
        {
            var progress = buf.ReadFloat();
            var level = buf.ReadVarInt();
            var total = buf.ReadVarInt();
            return new(progress, total, level);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSetExperiencePacket value)
        {
            buf.WriteFloat(value.ExperienceProgress);
            buf.WriteVarInt(value.ExperienceLevel);
            buf.WriteVarInt(value.TotalExperience);
        }
    }
}
