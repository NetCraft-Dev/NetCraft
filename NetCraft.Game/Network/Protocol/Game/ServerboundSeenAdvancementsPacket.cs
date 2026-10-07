using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSeenAdvancementsPacket seen advancements packet, maps to vanilla ServerboundSeenAdvancementsPacket
//Fields: Action(int, 0 opened tab, 1 closed screen), Tab(Identifier, carried only when opening a tab)
public sealed record ServerboundSeenAdvancementsPacket(int Action, Identifier? Tab) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSeenAdvancementsPacket> StreamCodec { get; } = new SeenAdvancementsCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSeenAdvancements;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSeenAdvancements(this);

    private sealed class SeenAdvancementsCodec : StreamCodec<FriendlyByteBuf, ServerboundSeenAdvancementsPacket>
    {
        //Sent when the advancements screen opens or closes; only Action=OPENED_TAB carries the tab identifier
        public ServerboundSeenAdvancementsPacket Decode(FriendlyByteBuf buf)
        {
            var action = buf.ReadVarInt();
            Identifier? tab = action == 0 ? buf.ReadIdentifier() : null;
            return new(action, tab);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundSeenAdvancementsPacket value)
        {
            buf.WriteVarInt(value.Action);
            if (value.Action == 0) buf.WriteIdentifier(value.Tab!.Value);
        }
    }
}
