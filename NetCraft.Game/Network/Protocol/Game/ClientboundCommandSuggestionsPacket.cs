using NetCraft.Network.Chat;
using NetCraft.Network.Component;

namespace NetCraft.Game.Network.Protocol.Game;

//CommandSuggestionEntry 命令建议条目对应原版 ClientboundCommandSuggestionsPacket.Entry
//文本 + 可选 tooltip tooltip 按组件的 trusted optional 编码
public sealed record CommandSuggestionEntry(string Text, Component? Tooltip);

//ClientboundCommandSuggestionsPacket 命令建议包对应原版 ClientboundCommandSuggestionsPacket
//字段 Id(int) Start(int) Length(int) Entries(条目列表)
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
