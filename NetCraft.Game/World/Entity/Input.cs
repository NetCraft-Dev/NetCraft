using NetCraft.Network;

namespace NetCraft.Game.World.Entity;

//Input 玩家按键状态对应原版 net.minecraft.world.entity.player.Input
//七个按键压进一个字节 空实例表示全部松开
public sealed record Input(
    bool Forward,
    bool Backward,
    bool Left,
    bool Right,
    bool Jump,
    bool Shift,
    bool Sprint)
{
    //Empty 全部松开 对应原版 EMPTY
    public static readonly Input Empty = new(false, false, false, false, false, false, false);

    //StreamCodec 单字节位标志 对应原版 STREAM_CODEC
    public static readonly StreamCodec<FriendlyByteBuf, Input> StreamCodec = new InputStreamCodec();
}

//InputStreamCodec 七位标志编解码 对应原版 STREAM_CODEC
internal sealed class InputStreamCodec : StreamCodec<FriendlyByteBuf, Input>
{
    public Input Decode(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        return new Input(
            (flags & 1) != 0,
            (flags & 2) != 0,
            (flags & 4) != 0,
            (flags & 8) != 0,
            (flags & 16) != 0,
            (flags & 32) != 0,
            (flags & 64) != 0);
    }

    public void Encode(FriendlyByteBuf buf, Input value)
    {
        var flags = 0;
        if (value.Forward) flags |= 1;
        if (value.Backward) flags |= 2;
        if (value.Left) flags |= 4;
        if (value.Right) flags |= 8;
        if (value.Jump) flags |= 16;
        if (value.Shift) flags |= 32;
        if (value.Sprint) flags |= 64;
        buf.WriteByte((byte)flags);
    }
}
