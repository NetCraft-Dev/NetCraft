using NetCraft.Codec;
using NetCraft.Game.World.Level.Block.Entity;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//Bees list of bees stored in a bee nest item, maps to vanilla net.minecraft.world.item.component.Bees
public sealed record Bees(IReadOnlyList<Occupant> Occupants)
{
    public static readonly Bees Empty = new(Array.Empty<Occupant>());

    //Codec persistence codec, maps to vanilla Bees.CODEC
    public static readonly Codec<Bees> Codec = Occupant.ListCodec.ComapFlatMap(
        occupants => DataResult<Bees>.Success(new Bees(occupants)),
        bees => bees.Occupants);

    public static readonly StreamCodec<RegistryFriendlyByteBuf, Bees> StreamCodec = new BeesStreamCodec();
}

//BeesStreamCodec maps to vanilla STREAM_CODEC, writes the count then each bee
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
