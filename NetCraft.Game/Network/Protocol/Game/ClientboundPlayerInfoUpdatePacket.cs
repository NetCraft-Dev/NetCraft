using NetCraft.Game.World.Level;
using NetCraft.Network;
using NetCraft.Network.Chat;

namespace NetCraft.Game.Network.Protocol.Game;

//PlayerInfoAction 玩家信息更新动作位掩码对应原版 ClientboundPlayerInfoUpdatePacket.Action
public enum PlayerInfoAction : byte
{
    AddPlayer = 0,
    InitializeChat = 1,
    UpdateGameMode = 2,
    UpdateListed = 3,
    UpdateLatency = 4,
    UpdateDisplayName = 5
}

//PlayerInfoEntry 玩家信息条目对应原版 ClientboundPlayerInfoUpdatePacket.Entry
//S2 支持 AddPlayer(无 properties)+UpdateGameMode/UpdateListed/UpdateLatency/UpdateDisplayName(null)
public sealed record PlayerInfoEntry(
    Guid ProfileId,
    string Name,
    GameType GameMode,
    bool Listed,
    int Latency,
    Component? DisplayName)
{
    public static StreamCodec<FriendlyByteBuf, PlayerInfoEntry> StaticCodec { get; } = new EntryCodec();

    //EncodeEntry 按 actions 位掩码顺序写条目数据
    public static void EncodeEntry(FriendlyByteBuf buf, PlayerInfoEntry entry, int actions)
    {
        buf.WriteUuid(entry.ProfileId);
        if ((actions & (1 << (int)PlayerInfoAction.AddPlayer)) != 0)
        {
            buf.WriteString(entry.Name);
            //properties 列表 0 个 S2 不实现签名属性
            buf.WriteVarInt(0);
        }
        if ((actions & (1 << (int)PlayerInfoAction.InitializeChat)) != 0)
            throw new NotSupportedException("InitializeChat 未实现");
        if ((actions & (1 << (int)PlayerInfoAction.UpdateGameMode)) != 0)
            buf.WriteVarInt(entry.GameMode.Id);
        if ((actions & (1 << (int)PlayerInfoAction.UpdateListed)) != 0)
            buf.WriteBoolean(entry.Listed);
        if ((actions & (1 << (int)PlayerInfoAction.UpdateLatency)) != 0)
            buf.WriteVarInt(entry.Latency);
        if ((actions & (1 << (int)PlayerInfoAction.UpdateDisplayName)) != 0)
        {
            //S2 只支持 null displayName Component 反序列化留后续
            buf.WriteBoolean(entry.DisplayName is not null);
            if (entry.DisplayName is not null)
                throw new NotSupportedException("DisplayName 序列化未实现");
        }
    }

    private sealed class EntryCodec : StreamCodec<FriendlyByteBuf, PlayerInfoEntry>
    {
        public PlayerInfoEntry Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Entry 解码由 EncodeEntry 静态方法对称实现");

        public void Encode(FriendlyByteBuf buf, PlayerInfoEntry value)
            => throw new NotImplementedException("Entry 编码由 EncodeEntry 静态方法对称实现");
    }
}

//ClientboundPlayerInfoUpdatePacket 玩家信息更新包对应原版 ClientboundPlayerInfoUpdatePacket
//actions 位掩码标识哪些动作被更新 entries 同 action 集合的玩家列表
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
                    throw new NotSupportedException("InitializeChat 解码未实现");
                if ((actions & (1 << (int)PlayerInfoAction.UpdateGameMode)) != 0)
                    gameMode = GameType.ById(buf.ReadVarInt()) ?? GameType.Survival;
                if ((actions & (1 << (int)PlayerInfoAction.UpdateListed)) != 0)
                    listed = buf.ReadBoolean();
                if ((actions & (1 << (int)PlayerInfoAction.UpdateLatency)) != 0)
                    latency = buf.ReadVarInt();
                if ((actions & (1 << (int)PlayerInfoAction.UpdateDisplayName)) != 0)
                {
                    if (buf.ReadBoolean())
                        throw new NotSupportedException("DisplayName 解码未实现");
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
