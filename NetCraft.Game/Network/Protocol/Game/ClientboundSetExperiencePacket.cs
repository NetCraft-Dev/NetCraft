namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetExperiencePacket experience packet, maps to vanilla ClientboundSetExperiencePacket
//Fields: ExperienceProgress(float), TotalExperience(int), ExperienceLevel(int)
public sealed record ClientboundSetExperiencePacket(float ExperienceProgress, int TotalExperience, int ExperienceLevel) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetExperiencePacket> StreamCodec { get; } = new SetExperienceCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetExperience;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetExperience(this);

    private sealed class SetExperienceCodec : StreamCodec<FriendlyByteBuf, ClientboundSetExperiencePacket>
    {
        //S4 vanilla order: progress(float) level(varint) total(varint); note total is not first
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
