namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameEventPacket 游戏事件包对应原版 ClientboundGameEventPacket
//Event 为事件类型 Param 为事件参数 参数语义随事件而定
public sealed record ClientboundGameEventPacket(GameEventType Event, float Param) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundGameEventPacket> StreamCodec { get; } = new GameEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundGameEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleGameEvent(this);

    private sealed class GameEventCodec : StreamCodec<FriendlyByteBuf, ClientboundGameEventPacket>
    {
        public ClientboundGameEventPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadByte();
            var param = buf.ReadFloat();
            return new ClientboundGameEventPacket(GameEventType.ById(id), param);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundGameEventPacket value)
        {
            buf.WriteByte((byte)value.Event.Id);
            buf.WriteFloat(value.Param);
        }
    }
}

//GameEventType 游戏事件类型对应原版 ClientboundGameEventPacket.Type
//Id 为线缆上的事件编号 未知编号保留原值不丢弃以便进一步排查
public sealed class GameEventType
{
    private static readonly GameEventType[] ByIdTable = new GameEventType[14];

    public static readonly GameEventType NoRespawnBlockAvailable = Register(0, "no_respawn_block_available");
    public static readonly GameEventType StartRaining = Register(1, "start_raining");
    public static readonly GameEventType StopRaining = Register(2, "stop_raining");
    public static readonly GameEventType ChangeGameMode = Register(3, "change_game_mode");
    public static readonly GameEventType WinGame = Register(4, "win_game");
    public static readonly GameEventType DemoEvent = Register(5, "demo_event");
    public static readonly GameEventType ArrowHitPlayer = Register(6, "arrow_hit_player");
    public static readonly GameEventType RainLevelChange = Register(7, "rain_level_change");
    public static readonly GameEventType ThunderLevelChange = Register(8, "thunder_level_change");
    public static readonly GameEventType PufferFishSting = Register(9, "puffer_fish_sting");
    public static readonly GameEventType GuardianElderEffect = Register(10, "guardian_elder_effect");
    public static readonly GameEventType ImmediateRespawn = Register(11, "immediate_respawn");
    public static readonly GameEventType LimitedCrafting = Register(12, "limited_crafting");
    public static readonly GameEventType LevelChunksLoadStart = Register(13, "level_chunks_load_start");

    public int Id { get; }
    public string Name { get; }

    private GameEventType(int id, string name)
    {
        Id = id;
        Name = name;
    }

    private static GameEventType Register(int id, string name)
    {
        var type = new GameEventType(id, name);
        ByIdTable[id] = type;
        return type;
    }

    //ById 按线缆编号取事件类型 未登记的编号原样保留
    public static GameEventType ById(int id)
        => id >= 0 && id < ByIdTable.Length && ByIdTable[id] is not null
            ? ByIdTable[id]
            : new GameEventType(id, $"unknown_{id}");

    public override string ToString() => Name;
}
