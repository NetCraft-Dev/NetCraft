namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundClientCommandPacket client command packet, maps to vanilla ServerboundClientCommandPacket
//Field: Action(ClientCommandAction), e.g. request respawn / request stats / request game rule values
public sealed record ServerboundClientCommandPacket(ClientCommandAction Action) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundClientCommandPacket> StreamCodec { get; } = new ClientCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundClientCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandleClientCommand(this);

    private sealed class ClientCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundClientCommandPacket>
    {
        //Vanilla writeEnum(action), i.e. a VarInt enum ordinal
        public ServerboundClientCommandPacket Decode(FriendlyByteBuf buf)
            => new((ClientCommandAction)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundClientCommandPacket value)
            => buf.WriteVarInt((int)value.Action);
    }
}

//ClientCommandAction client command action, maps to vanilla ServerboundClientCommandPacket.Action
public enum ClientCommandAction
{
    PerformRespawn,
    RequestStats,
    RequestGameRuleValues
}
