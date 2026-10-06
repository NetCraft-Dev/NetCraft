using System.Collections.Generic;
using System.IO;
using System.Text;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Configuration;

//BiomeRegistrySynchronization aligns with vanilla RegistrySynchronization
//Packs BuiltInRegistries.BIOME into the ClientboundRegistryDataPacket.Entries bytes
//entries = VarInt(count) + PackedRegistryEntry... each entry = id(utf) + optional(Tag)
//Maps to vanilla PackedRegistryEntry.STREAM_CODEC.apply(ByteBufCodecs.list())
public static class BiomeRegistrySynchronization
{
    //EncodeBiome encodes a Biome into a CompoundTag shaped like NETWORK_CODEC
    //Aligns with Biome.NETWORK_CODEC, coding only climate(has_precipitation/temperature/downfall) + effects(watter_color)
    //temperature_modifier defaults to none and attributes to EMPTY, both omitted
    public static CompoundTag EncodeBiome(Biome biome)
    {
        var tag = new CompoundTag();
        tag.PutByte("has_precipitation", biome.Climate.HasPrecipitation ? (byte)1 : (byte)0);
        tag.PutFloat("temperature", biome.Climate.Temperature);
        tag.PutFloat("downfall", biome.Climate.Downfall);
        var effects = new CompoundTag();
        effects.PutString("water_color", biome.Effects.WaterColorHex);
        tag.Put("effects", effects);
        return tag;
    }

    //PackBiomes packs the biome registry into registry_data entries bytes in registration order
    //Each entry writes id(utf string) + presence flag(1 byte) + anyTag(type byte + payload with no root name)
    //Vanilla goes through FriendlyByteBuf.writeNbt → NbtIo.writeAnyTag with a root name, which would misalign
    public static byte[] PackBiomes(IEnumerable<KeyValuePair<ResourceKey<Biome>, Biome>> entries)
    {
        var list = new List<KeyValuePair<ResourceKey<Biome>, Biome>>(entries);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteVarInt(writer, list.Count);
        foreach (var kv in list)
        {
            WriteUtf(writer, kv.Key.Identifier.ToString());
            writer.Write((byte)1);
            NbtIo.WriteAnyTag(EncodeBiome(kv.Value), new BinaryNbtWriter(writer));
        }
        writer.Flush();
        return stream.ToArray();
    }

    //PackEmptyEntries packs registry entries bytes with only ids and no contents
    //Each entry writes id(utf) + presence flag 0; the client loads the actual data from local vanilla resources (known pack mechanism)
    //The server only needs to control the id list and order, the data is provided by the client's local jar
    public static byte[] PackEmptyEntries(IEnumerable<string> ids)
    {
        var list = new List<string>(ids);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteVarInt(writer, list.Count);
        foreach (var id in list)
        {
            WriteUtf(writer, id);
            writer.Write((byte)0);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteVarInt(BinaryWriter writer, int value)
    {
        var v = (uint)value;
        while ((v & ~0x7Fu) != 0)
        {
            writer.Write((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }
        writer.Write((byte)v);
    }

    private static void WriteUtf(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(writer, bytes.Length);
        writer.Write(bytes);
    }
}
