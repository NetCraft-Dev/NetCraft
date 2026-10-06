using NetCraft.Game.World.Level;
using NetCraft.Network;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Network.Protocol.Game;

//PlayerInfoAction player info update action bitmask, maps to vanilla ClientboundPlayerInfoUpdatePacket.Action
public enum PlayerInfoAction : byte
{
    AddPlayer = 0,
    InitializeChat = 1,
    UpdateGameMode = 2,
    UpdateListed = 3,
    UpdateLatency = 4,
    UpdateDisplayName = 5
}

//PlayerInfoEntry player info entry, maps to vanilla ClientboundPlayerInfoUpdatePacket.Entry
//S2 supports AddPlayer (no properties) + UpdateGameMode/UpdateListed/UpdateLatency/UpdateDisplayName (null)
public sealed record PlayerInfoEntry(
    Guid ProfileId,
    string Name,
    GameType GameMode,
    bool Listed,
    int Latency,
    Component? DisplayName)
{
    public static StreamCodec<FriendlyByteBuf, PlayerInfoEntry> StaticCodec { get; } = new EntryCodec();

    //EncodeEntry writes entry data in the order of the actions bitmask
    public static void EncodeEntry(FriendlyByteBuf buf, PlayerInfoEntry entry, int actions)
    {
        buf.WriteUuid(entry.ProfileId);
        if ((actions & (1 << (int)PlayerInfoAction.AddPlayer)) != 0)
        {
            buf.WriteString(entry.Name);
            //properties list of 0; S2 does not implement signed properties
            buf.WriteVarInt(0);
        }
        if ((actions & (1 << (int)PlayerInfoAction.InitializeChat)) != 0)
            throw new NotSupportedException("InitializeChat is not implemented");
        if ((actions & (1 << (int)PlayerInfoAction.UpdateGameMode)) != 0)
            buf.WriteVarInt(entry.GameMode.Id);
        if ((actions & (1 << (int)PlayerInfoAction.UpdateListed)) != 0)
            buf.WriteBoolean(entry.Listed);
        if ((actions & (1 << (int)PlayerInfoAction.UpdateLatency)) != 0)
            buf.WriteVarInt(entry.Latency);
        if ((actions & (1 << (int)PlayerInfoAction.UpdateDisplayName)) != 0)
        {
            //S2 only supports a null displayName; Component deserialization is left for later
            buf.WriteBoolean(entry.DisplayName is not null);
            if (entry.DisplayName is not null)
                throw new NotSupportedException("DisplayName serialization is not implemented");
        }
    }

    private sealed class EntryCodec : StreamCodec<FriendlyByteBuf, PlayerInfoEntry>
    {
        public PlayerInfoEntry Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Entry decoding is implemented symmetrically by the EncodeEntry static method");

        public void Encode(FriendlyByteBuf buf, PlayerInfoEntry value)
            => throw new NotImplementedException("Entry encoding is implemented symmetrically by the EncodeEntry static method");
    }
}

//ClientboundPlayerInfoUpdatePacket player info update packet, maps to vanilla ClientboundPlayerInfoUpdatePacket
//The actions bitmask marks which actions are being updated; entries is the player list for the same action set
public sealed record ClientboundPlayerInfoUpdatePacket(int Actions, IReadOnlyList<PlayerInfoEntry> Entries) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoUpdatePacket> StreamCodec { get; } = new PlayerInfoUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerInfoUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerInfoUpdate(this);

    private sealed class PlayerInfoUpdateCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoUpdatePacket>
    {
        public ClientboundPlayerInfoUpdatePacket Decode(FriendlyByteBuf buf)
        {
            var actions = buf.ReadByte();
            var count = buf.ReadVarInt();
            var entries = new PlayerInfoEntry[count];
            for (var i = 0; i < count; i++)
            {
                var profileId = buf.ReadUuid();
                string? name = null;
                GameType gameMode = GameType.Survival;
                var listed = false;
                var latency = 0;
                Component? displayName = null;
                if ((actions & (1 << (int)PlayerInfoAction.AddPlayer)) != 0)
                {
                    name = buf.ReadString();
                    var propertyCount = buf.ReadVarInt();
                    for (var p = 0; p < propertyCount; p++)
                    {
                        buf.ReadString();
                        buf.ReadString();
                        if (buf.ReadBoolean()) buf.ReadByteArray();
                    }
                }
                if ((actions & (1 << (int)PlayerInfoAction.InitializeChat)) != 0)
                    throw new NotSupportedException("InitializeChat decoding is not implemented");
                if ((actions & (1 << (int)PlayerInfoAction.UpdateGameMode)) != 0)
                    gameMode = GameType.ById(buf.ReadVarInt()) ?? GameType.Survival;
                if ((actions & (1 << (int)PlayerInfoAction.UpdateListed)) != 0)
                    listed = buf.ReadBoolean();
                if ((actions & (1 << (int)PlayerInfoAction.UpdateLatency)) != 0)
                    latency = buf.ReadVarInt();
                if ((actions & (1 << (int)PlayerInfoAction.UpdateDisplayName)) != 0)
                {
                    if (buf.ReadBoolean())
                        throw new NotSupportedException("DisplayName decoding is not implemented");
                }
                entries[i] = new PlayerInfoEntry(profileId, name ?? string.Empty, gameMode, listed, latency, displayName);
            }
            return new ClientboundPlayerInfoUpdatePacket(actions, entries);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerInfoUpdatePacket value)
        {
            buf.WriteByte((byte)value.Actions);
            buf.WriteVarInt(value.Entries.Count);
            foreach (var entry in value.Entries)
                PlayerInfoEntry.EncodeEntry(buf, entry, value.Actions);
        }
    }
}
