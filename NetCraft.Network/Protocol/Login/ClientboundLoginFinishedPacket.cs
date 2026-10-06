namespace NetCraft.Network.Protocol.Login;

//ClientboundLoginFinishedPacket server login finished packet, maps to vanilla net.minecraft.network.protocol.login.ClientboundLoginFinishedPacket
//Contains GameProfile and sessionId UUID
//IsTerminal true means login is complete and it switches to CONFIGURATION
public sealed record ClientboundLoginFinishedPacket(GameProfile GameProfile, Guid SessionId) : Packet<ClientLoginPacketListener>
{
    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginFinishedPacket> StreamCodec { get; } = new FinishedCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundLoginFinished;

    //IsTerminal switches to CONFIGURATION after login completes
    public bool IsTerminal => true;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleLoginFinished(this);

    //FinishedCodec codec reading and writing GameProfile(uuid+name+properties) + sessionId UUID
    //S4 aligns with vanilla ByteBufCodecs.GAME_PROFILE = UUID + PLAYER_NAME + GAME_PROFILE_PROPERTIES
    private sealed class FinishedCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginFinishedPacket>
    {
        public ClientboundLoginFinishedPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadUuid();
            var name = buf.ReadString(16);
            var propertyCount = buf.ReadVarInt();
            for (var i = 0; i < propertyCount; i++)
            {
                buf.ReadString();
                buf.ReadString();
                if (buf.ReadBoolean()) buf.ReadByteArray();
            }
            var sessionId = buf.ReadUuid();
            return new ClientboundLoginFinishedPacket(new GameProfile(id, name), sessionId);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundLoginFinishedPacket value)
        {
            buf.WriteUuid(value.GameProfile.Id);
            buf.WriteString(value.GameProfile.Name, 16);
            //properties an empty list, 0 entries; S4 does not implement signed properties
            buf.WriteVarInt(0);
            buf.WriteUuid(value.SessionId);
        }
    }
}
