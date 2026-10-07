using NetCraft.Network;

namespace NetCraft.Game.World.Entity;

//Input player key states, maps to vanilla net.minecraft.world.entity.player.Input
//Seven keys packed into one byte, an empty instance means all released
public sealed record Input(
    bool Forward,
    bool Backward,
    bool Left,
    bool Right,
    bool Jump,
    bool Shift,
    bool Sprint)
{
    //Empty all released, maps to vanilla EMPTY
    public static readonly Input Empty = new(false, false, false, false, false, false, false);

    //StreamCodec single byte bit flags, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<FriendlyByteBuf, Input> StreamCodec = new InputStreamCodec();
}

//InputStreamCodec seven bit flag codec, maps to vanilla STREAM_CODEC
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
