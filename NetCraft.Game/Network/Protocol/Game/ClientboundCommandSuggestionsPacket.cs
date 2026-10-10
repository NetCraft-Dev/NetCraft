using NetCraft.Network.Chat;
using NetCraft.Game.World.Items.Component;

namespace NetCraft.Game.Network.Protocol.Game;

//CommandSuggestionEntry command suggestion entry, maps to vanilla ClientboundCommandSuggestionsPacket.Entry
//Text + optional tooltip; the tooltip is encoded as a trusted optional component
public sealed record CommandSuggestionEntry(string Text, Component? Tooltip);

//ClientboundCommandSuggestionsPacket command suggestions packet, maps to vanilla ClientboundCommandSuggestionsPacket
//Fields: Id(int), Start(int), Length(int), Entries (entry list)
public sealed record ClientboundCommandSuggestionsPacket(
    int Id, int Start, int Length, IReadOnlyList<CommandSuggestionEntry> Entries) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCommandSuggestionsPacket> StreamCodec { get; } = new CommandSuggestionsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCommandSuggestions;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCommandSuggestions(this);

    private sealed class CommandSuggestionsCodec : StreamCodec<FriendlyByteBuf, ClientboundCommandSuggestionsPacket>
    {
        public ClientboundCommandSuggestionsPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadVarInt();
            var start = buf.ReadVarInt();
            var length = buf.ReadVarInt();
            var count = buf.ReadVarInt();
            var entries = new List<CommandSuggestionEntry>(count);
            for (var i = 0; i < count; i++)
            {
                var text = buf.ReadString();
                var tooltip = buf.ReadBoolean() ? ComponentSerialization.StreamCodec.Decode(buf) : null;
                entries.Add(new CommandSuggestionEntry(text, tooltip));
            }
            return new ClientboundCommandSuggestionsPacket(id, start, length, entries);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundCommandSuggestionsPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteVarInt(value.Start);
            buf.WriteVarInt(value.Length);
            buf.WriteVarInt(value.Entries.Count);
            foreach (var entry in value.Entries)
            {
                buf.WriteString(entry.Text);
                buf.WriteBoolean(entry.Tooltip is not null);
                if (entry.Tooltip is not null)
                    ComponentSerialization.StreamCodec.Encode(buf, entry.Tooltip);
            }
        }
    }
}
