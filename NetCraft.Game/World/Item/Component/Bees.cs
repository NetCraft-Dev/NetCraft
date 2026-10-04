using NetCraft.Game.World.Level.Block.Entity;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//Bees 蜂巢物品里装的蜜蜂列表 对应原版 net.minecraft.world.item.component.Bees
public sealed record Bees(IReadOnlyList<Occupant> Occupants)
{
    public static readonly Bees Empty = new(Array.Empty<Occupant>());

    public static readonly StreamCodec<RegistryFriendlyByteBuf, Bees> StreamCodec = new BeesStreamCodec();
}

//BeesStreamCodec 对应原版 STREAM_CODEC 先写数量再逐个写蜜蜂
internal sealed class BeesStreamCodec : StreamCodec<RegistryFriendlyByteBuf, Bees>
{
    public Bees Decode(RegistryFriendlyByteBuf buf)
    {
        var count = buf.ReadVarInt();
        var occupants = new List<Occupant>(count);
        for (var i = 0; i < count; i++) occupants.Add(Occupant.StreamCodec.Decode(buf));
        return new Bees(occupants);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Bees value)
    {
        buf.WriteVarInt(value.Occupants.Count);
        foreach (var occupant in value.Occupants) Occupant.StreamCodec.Encode(buf, occupant);
    }
}
