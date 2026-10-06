using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Light;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLightUpdatePacketData light update data, maps to vanilla ClientboundLightUpdatePacketData
//S4 26.2 format: skyYMask + blockYMask + emptySkyYMask + emptyBlockYMask (BitSet) + skyUpdates + blockUpdates (list)
//BitSet serialization = varint long count + each long big-endian 8 bytes; each DataLayer entry = varint length + 2048 bytes
//mask marks sections with data, emptyMask marks sections with empty data; sections in neither keep their client-side value
public sealed record ClientboundLightUpdatePacketData(
    byte[] SkyYMask,
    byte[] BlockYMask,
    byte[] EmptySkyYMask,
    byte[] EmptyBlockYMask,
    byte[][] SkyUpdates,
    byte[][] BlockUpdates)
{
    //Empty empty light data, used when there is no light
    public static ClientboundLightUpdatePacketData Empty { get; }
        = new(Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>(),
            Array.Empty<byte[]>(), Array.Empty<byte[]>());

    //Builds full-chunk send data from the light engine, maps to vanilla ClientboundLightUpdatePacketData(ChunkPos, LevelLightEngine, BitSet, BitSet)
    //A null filter means all sections of that layer are sent; incremental sends pass a changed bitmap
    public ClientboundLightUpdatePacketData(ChunkPos chunkPos, LevelLightEngine lightEngine,
        byte[]? skyFilter = null, byte[]? blockFilter = null)
        : this(Prepare(chunkPos, lightEngine, skyFilter, blockFilter)) { }

    private ClientboundLightUpdatePacketData(LightDataBuild build)
        : this(build.SkyMask, build.BlockMask, build.EmptySkyMask, build.EmptyBlockMask,
            build.SkyUpdates, build.BlockUpdates) { }

    //Write writes to the FriendlyByteBuf, aligning with vanilla ClientboundLightUpdatePacketData serialization
    public void Write(FriendlyByteBuf buf)
    {
        WriteBitSet(buf, SkyYMask);
        WriteBitSet(buf, BlockYMask);
        WriteBitSet(buf, EmptySkyYMask);
        WriteBitSet(buf, EmptyBlockYMask);
        WriteDataLayers(buf, SkyUpdates);
        WriteDataLayers(buf, BlockUpdates);
    }

    //Read reads a ClientboundLightUpdatePacketData from the FriendlyByteBuf
    public static ClientboundLightUpdatePacketData Read(FriendlyByteBuf buf)
    {
        var skyYMask = ReadBitSet(buf);
        var blockYMask = ReadBitSet(buf);
        var emptySkyYMask = ReadBitSet(buf);
        var emptyBlockYMask = ReadBitSet(buf);
        var skyUpdates = ReadDataLayers(buf);
        var blockUpdates = ReadDataLayers(buf);
        return new ClientboundLightUpdatePacketData(skyYMask, blockYMask, emptySkyYMask, emptyBlockYMask,
            skyUpdates, blockUpdates);
    }

    //WriteBitSet writes a BitSet as varint long count + longs
    //Vanilla writeBitSet goes through BitSet.toLongArray + writeLongArray, each long big-endian
    //The internal byte[] stores bits in little-endian long layout, so each long must be converted to big-endian; writing straight in byte order would shift the bitmap onto the wrong sections
    private static void WriteBitSet(FriendlyByteBuf buf, byte[] bytes)
    {
        var longCount = bytes.Length / 8;
        buf.WriteVarInt(longCount);
        for (var i = 0; i < longCount; i++)
            buf.WriteLong(ReadLittleEndianLong(bytes, i * 8));
    }

    //ReadBitSet reads a BitSet: varint long count + longs
    private static byte[] ReadBitSet(FriendlyByteBuf buf)
    {
        var count = buf.ReadVarInt();
        var bytes = new byte[count * 8];
        for (var i = 0; i < count; i++)
            WriteLittleEndianLong(bytes, i * 8, buf.ReadLong());
        return bytes;
    }

    //ReadLittleEndianLong reassembles 8 bytes into a long little-endian
    private static long ReadLittleEndianLong(byte[] bytes, int offset)
    {
        long value = 0;
        for (var i = 0; i < 8; i++)
            value |= (long)bytes[offset + i] << (i * 8);
        return value;
    }

    //WriteLittleEndianLong splits a long back into 8 bytes little-endian
    private static void WriteLittleEndianLong(byte[] bytes, int offset, long value)
    {
        for (var i = 0; i < 8; i++)
            bytes[offset + i] = (byte)(value >> (i * 8));
    }

    //WriteDataLayers writes a DataLayer list: varint count + each entry varint length + 2048 bytes
    //The vanilla DATA_LAYER_STREAM_CODEC is ByteBufCodecs.byteArray(2048), i.e. writeByteArray with a length prefix
    private static void WriteDataLayers(FriendlyByteBuf buf, byte[][] layers)
    {
        buf.WriteVarInt(layers.Length);
        foreach (var layer in layers)
        {
            buf.WriteVarInt(layer.Length);
            buf.WriteBytes(layer);
        }
    }

    //ReadDataLayers reads a DataLayer list: varint count + each entry varint length + data
    private static byte[][] ReadDataLayers(FriendlyByteBuf buf)
    {
        var count = buf.ReadVarInt();
        var layers = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            var length = buf.ReadVarInt();
            layers[i] = buf.ReadBytes(length);
        }
        return layers;
    }

    //LightDataBuild build result of both light layers
    private sealed record LightDataBuild(byte[] SkyMask, byte[] BlockMask, byte[] EmptySkyMask,
        byte[] EmptyBlockMask, byte[][] SkyUpdates, byte[][] BlockUpdates);

    //LayerBuild build result of a single layer
    private sealed record LayerBuild(byte[] Mask, byte[] EmptyMask, byte[][] Updates);

    //Prepare fetches data per layer from the light engine, maps to the vanilla constructor
    private static LightDataBuild Prepare(ChunkPos pos, LevelLightEngine lightEngine,
        byte[]? skyFilter, byte[]? blockFilter)
    {
        var sky = PrepareLayer(pos, lightEngine, LightLayer.Sky, skyFilter);
        var block = PrepareLayer(pos, lightEngine, LightLayer.Block, blockFilter);
        return new LightDataBuild(sky.Mask, block.Mask, sky.EmptyMask, block.EmptyMask, sky.Updates, block.Updates);
    }

    //PrepareLayer fetches layer data per light section; a null from getDataLayerData means the section is disabled and not sent
    //Sections with data set mask and collect 2048 bytes; empty data sets emptyMask so the client clears the section
    private static LayerBuild PrepareLayer(ChunkPos pos, LevelLightEngine lightEngine, LightLayer layer,
        byte[]? filter)
    {
        var count = lightEngine.GetLightSectionCount();
        var mask = new byte[(count + 63) / 64 * 8];
        var emptyMask = new byte[mask.Length];
        var updates = new List<byte[]>();
        var listener = lightEngine.GetLayerListener(layer);
        var minLightSection = lightEngine.GetMinLightSection();
        for (var sectionIndex = 0; sectionIndex < count; sectionIndex++)
        {
            if (filter is not null && !IsSet(filter, sectionIndex)) continue;
            var data = listener.GetDataLayerData(new SectionPos(pos.X, minLightSection + sectionIndex, pos.Z));
            if (data is null) continue;
            if (data.IsEmpty)
            {
                SetBit(emptyMask, sectionIndex);
            }
            else
            {
                SetBit(mask, sectionIndex);
                //Copy the bytes so the engine cannot keep mutating the same memory during the send
                updates.Add(data.Copy().GetData());
            }
        }
        return new LayerBuild(TrimBitSet(mask), TrimBitSet(emptyMask), updates.ToArray());
    }

    //CreateFilter builds a filter bitmap from the affected section indices; an empty set returns an empty bitmap meaning no change on that layer
    public static byte[] CreateFilter(int sectionCount, IReadOnlyList<int> indices)
    {
        if (indices.Count == 0) return Array.Empty<byte>();
        var bits = new byte[(sectionCount + 63) / 64 * 8];
        foreach (var index in indices) SetBit(bits, index);
        return TrimBitSet(bits);
    }

    //IsSet reads a mask bit by section index, used by the client to restore light for each section
    public static bool IsSet(byte[] bits, int index)
        => index >= 0 && index / 8 < bits.Length
           && (bits[(index >> 6) * 8 + ((index >> 3) & 7)] & (1 << (index & 7))) != 0;

    //SetBit sets a bit in the vanilla BitSet little-endian long layout
    private static void SetBit(byte[] bits, int index)
        => bits[(index >> 6) * 8 + ((index >> 3) & 7)] |= (byte)(1 << (index & 7));

    //TrimBitSet drops trailing all-zero longs to match the shortest form of vanilla BitSet.toLongArray
    private static byte[] TrimBitSet(byte[] bits)
    {
        var length = bits.Length;
        while (length > 0)
        {
            var allZero = true;
            for (var i = length - 8; i < length; i++)
            {
                if (bits[i] == 0) continue;
                allZero = false;
                break;
            }
            if (!allZero) break;
            length -= 8;
        }
        return length == bits.Length ? bits : bits[..length];
    }
}
