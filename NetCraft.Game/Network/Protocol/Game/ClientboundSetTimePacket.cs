namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetTimePacket 时间设置包对应原版 ClientboundSetTimePacket
//字段 GameTime(long) ClockUpdates(Map<Holder<WorldClock>, ClockNetworkState>)
//网络格式 gameTime 定长 long + map(varint 个数 + holderRegistry(varint id) + VAR_LONG totalTicks + float partialTick + float rate)
public sealed record ClientboundSetTimePacket(long GameTime, IReadOnlyList<ClientboundSetTimePacket.ClockUpdate> ClockUpdates)
    : Packet<ClientGamePacketListener>
{
    //ClockUpdate 单个世界时钟的网络状态对应原版 ClockNetworkState
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
