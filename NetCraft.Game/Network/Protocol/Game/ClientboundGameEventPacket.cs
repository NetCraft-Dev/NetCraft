namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameEventPacket game event packet, maps to vanilla ClientboundGameEventPacket
//Event is the event type, Param is the event parameter; the meaning of the parameter depends on the event
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

//GameEventType game event type, maps to vanilla ClientboundGameEventPacket.Type
//Id is the event number on the wire; unknown numbers keep their raw value rather than being dropped, for easier troubleshooting
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

    //ById looks up the event type by wire number; unregistered numbers are preserved as-is
    public static GameEventType ById(int id)
        => id >= 0 && id < ByIdTable.Length && ByIdTable[id] is not null
            ? ByIdTable[id]
            : new GameEventType(id, $"unknown_{id}");

    public override string ToString() => Name;
}
