namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTimePacket set time packet, maps to vanilla ClientboundSetTimePacket
//Fields: GameTime(long), ClockUpdates(Map<Holder<WorldClock>, ClockNetworkState>)
//Network format: gameTime fixed-length long + map (varint count + holderRegistry (varint id) + VAR_LONG totalTicks + float partialTick + float rate)
public sealed record ClientboundSetTimePacket(long GameTime, IReadOnlyList<ClientboundSetTimePacket.ClockUpdate> ClockUpdates)
    : Packet<ClientGamePacketListener>
{
    //ClockUpdate network state of a single world clock, maps to vanilla ClockNetworkState
    public readonly record struct ClockUpdate(int ClockId, long TotalTicks, float PartialTick, float Rate);

    public static StreamCodec<FriendlyByteBuf, ClientboundSetTimePacket> StreamCodec { get; } = new SetTimeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetTime;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetTime(this);

    private sealed class SetTimeCodec : StreamCodec<FriendlyByteBuf, ClientboundSetTimePacket>
    {
        public ClientboundSetTimePacket Decode(FriendlyByteBuf buf)
        {
            var gameTime = buf.ReadLong();
            var count = buf.ReadVarInt();
            var updates = new List<ClockUpdate>(count);
            for (var i = 0; i < count; i++)
                updates.Add(new ClockUpdate(buf.ReadVarInt(), buf.ReadVarLong(), buf.ReadFloat(), buf.ReadFloat()));
            return new ClientboundSetTimePacket(gameTime, updates);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSetTimePacket value)
        {
            buf.WriteLong(value.GameTime);
            buf.WriteVarInt(value.ClockUpdates.Count);
            foreach (var update in value.ClockUpdates)
            {
                buf.WriteVarInt(update.ClockId);
                buf.WriteVarLong(update.TotalTicks);
                buf.WriteFloat(update.PartialTick);
                buf.WriteFloat(update.Rate);
            }
        }
    }
}
