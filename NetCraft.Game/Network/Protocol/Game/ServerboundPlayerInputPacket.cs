namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerInputPacket 玩家输入状态包对应原版 ServerboundPlayerInputPacket
//26.2 客户端每 tick 携带键盘状态 单字节位掩码编码
public sealed record ServerboundPlayerInputPacket(PlayerInput Input) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerInputPacket> StreamCodec { get; } = new PlayerInputCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerInput;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerInput(this);

    private sealed class PlayerInputCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerInputPacket>
    {
        public ServerboundPlayerInputPacket Decode(FriendlyByteBuf buf)
            => new(PlayerInput.Read(buf));

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerInputPacket value)
            => value.Input.Write(buf);
    }
}

//PlayerInput 键盘输入位掩码对应原版 net.minecraft.world.entity.player.Input
//位序与原版 FLAG_* 常量一致 前后左右跳跃潜行疾跑各占一位
public readonly record struct PlayerInput(
    bool Forward, bool Backward, bool Left, bool Right, bool Jump, bool Shift, bool Sprint)
{
    public static PlayerInput Read(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        return new(
            (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0,
            (flags & 16) != 0, (flags & 32) != 0, (flags & 64) != 0);
    }

    public void Write(FriendlyByteBuf buf)
        => buf.WriteByte((byte)((Forward ? 1 : 0) | (Backward ? 2 : 0) | (Left ? 4 : 0) | (Right ? 8 : 0)
            | (Jump ? 16 : 0) | (Shift ? 32 : 0) | (Sprint ? 64 : 0)));
}
