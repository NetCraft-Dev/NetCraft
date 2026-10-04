using System.Collections.Generic;
using System.IO;
using System.Text;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Configuration;

//BiomeRegistrySynchronization 对齐原版 RegistrySynchronization
//把 BuiltInRegistries.BIOME 打包为 ClientboundRegistryDataPacket.Entries 字节
//entries = VarInt(count) + PackedRegistryEntry... 每 entry = id(utf) + optional(Tag)
//对应原版 PackedRegistryEntry.STREAM_CODEC.apply(ByteBufCodecs.list())
public static class BiomeRegistrySynchronization
{
    //EncodeBiome 编码 Biome 为 NETWORK_CODEC 结构的 CompoundTag
    //对齐 Biome.NETWORK_CODEC 只编 climate(has_precipitation/temperature/downfall) + effects(watter_color)
    //temperature_modifier 默认 none attributes 默认 EMPTY 均省略
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

    //PackBiomes 按注册顺序把 biome 注册表打包为 registry_data entries 字节
    //每 entry 写 id(utf 字符串) + 存在标记(1 字节) + anyTag(type byte + payload 无 root name)
    //原版走 FriendlyByteBuf.writeNbt → NbtIo.writeAnyTag 带 root name 会错位
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

    //PackEmptyEntries 打包只有 id 无 contents 的注册表 entries 字节
    //每 entry 写 id(utf) + 存在标记 0 客户端从本地 vanilla 资源加载实际数据(known pack 机制)
    //服务端只需控制 id 列表与顺序 数据由客户端本地 jar 提供
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