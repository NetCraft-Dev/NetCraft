using NetCraft.Codec;
using NetCraft.Game.World.Items.Component;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block.Entity;

//Occupant saved data for one bee inside a hive, maps to vanilla BeehiveBlockEntity.Occupant
public sealed record Occupant(TypedEntityData<Holder<EntityType<object>>> EntityData, int TicksInHive, int MinTicksInHive)
{
    internal static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<EntityType<object>>> EntityTypeCodec
        = ByteBufCodecs.Holder(Registries.ENTITY_TYPE);

    internal static readonly StreamCodec<RegistryFriendlyByteBuf, TypedEntityData<Holder<EntityType<object>>>> EntityDataCodec
        = TypedEntityData<Holder<EntityType<object>>>.StreamCodecOf(EntityTypeCodec);

    internal static readonly Codec<TypedEntityData<Holder<EntityType<object>>>> EntityDataPersistentCodec
        = TypedEntityData<Holder<EntityType<object>>>.CodecOf(HolderSetCodecs.EntityTypeRef);

    //Codec persistent codec, maps to vanilla Occupant.CODEC
    public static readonly Codec<Occupant> Codec = RecordCodecBuilder.Of3(
        EntityDataPersistentCodec.FieldOf("entity_data").ForGetter((Occupant occupant) => occupant.EntityData),
        Codecs.Int.FieldOf("ticks_in_hive").ForGetter((Occupant occupant) => occupant.TicksInHive),
        Codecs.Int.FieldOf("min_ticks_in_hive").ForGetter((Occupant occupant) => occupant.MinTicksInHive),
        (entityData, ticksInHive, minTicksInHive) => new Occupant(entityData, ticksInHive, minTicksInHive));

    //ListCodec list codec, maps to vanilla Occupant.LIST_CODEC
    public static readonly Codec<IReadOnlyList<Occupant>> ListCodec = Codec.ListOf();

    public static readonly StreamCodec<RegistryFriendlyByteBuf, Occupant> StreamCodec = new OccupantStreamCodec();
}

//OccupantStreamCodec maps to vanilla STREAM_CODEC: entity data, ticks in hive, min ticks in hive
internal sealed class OccupantStreamCodec : StreamCodec<RegistryFriendlyByteBuf, Occupant>
{
    public Occupant Decode(RegistryFriendlyByteBuf buf)
    {
        var entityData = Occupant.EntityDataCodec.Decode(buf);
        var ticksInHive = buf.ReadVarInt();
        var minTicksInHive = buf.ReadVarInt();
        return new Occupant(entityData, ticksInHive, minTicksInHive);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Occupant value)
    {
        Occupant.EntityDataCodec.Encode(buf, value.EntityData);
        buf.WriteVarInt(value.TicksInHive);
        buf.WriteVarInt(value.MinTicksInHive);
    }
}
