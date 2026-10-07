namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerCommandPacket entity action packet, maps to vanilla ServerboundPlayerCommandPacket
//Entity state switches such as sprinting/sneaking/start riding jump/stopping riding jump; fields: Id(VarInt), Action(VarInt enum), Data(VarInt)
public sealed record ServerboundPlayerCommandPacket(int Id, PlayerCommandAction Action, int Data)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerCommandPacket> StreamCodec { get; } = new PlayerCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerCommand(this);

    private sealed class PlayerCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerCommandPacket>
    {
        public ServerboundPlayerCommandPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), (PlayerCommandAction)buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerCommandPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteVarInt((int)value.Action);
            buf.WriteVarInt(value.Data);
        }
    }
}

//PlayerCommandAction entity action enum, declaration order is the network ordinal and aligns with vanilla Action
public enum PlayerCommandAction
{
    StopSleeping,
    StartSprinting,
    StopSprinting,
    StartRidingJump,
    StopRidingJump,
    OpenInventory,
    StartFallFlying,
}
