using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Light;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLightUpdatePacketData 光照更新数据对应原版 ClientboundLightUpdatePacketData
//S4 26.2 格式: skyYMask + blockYMask + emptySkyYMask + emptyBlockYMask (BitSet) + skyUpdates + blockUpdates (list)
//BitSet 序列化 = varint longs 数量 + 每个 long 大端 8 字节 DataLayer 每项 = varint 长度 + 2048 字节
//mask 记有数据区段 emptyMask 记空数据区段 两者皆无的区段客户端保留原值
public sealed record ClientboundLightUpdatePacketData(
    byte[] SkyYMask,
    byte[] BlockYMask,
    byte[] EmptySkyYMask,
    byte[] EmptyBlockYMask,
    byte[][] SkyUpdates,
    byte[][] BlockUpdates)
{
    //Empty 空光照数据用于无光照场景
    public static ClientboundLightUpdatePacketData Empty { get; }
        = new(Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>(),
            Array.Empty<byte[]>(), Array.Empty<byte[]>());

    //从光照引擎构造整区块下发数据对应原版 ClientboundLightUpdatePacketData(ChunkPos, LevelLightEngine, BitSet, BitSet)
    //filter 为 null 表示该层所有区段都下发 增量下发时传变更位图
    public ClientboundLightUpdatePacketData(ChunkPos chunkPos, LevelLightEngine lightEngine,
        byte[]? skyFilter = null, byte[]? blockFilter = null)
        : this(Prepare(chunkPos, lightEngine, skyFilter, blockFilter)) { }

    private ClientboundLightUpdatePacketData(LightDataBuild build)
        : this(build.SkyMask, build.BlockMask, build.EmptySkyMask, build.EmptyBlockMask,
            build.SkyUpdates, build.BlockUpdates) { }

    //Write 写入 FriendlyByteBuf 对齐原版 ClientboundLightUpdatePacketData 序列化
    public void Write(FriendlyByteBuf buf)
    {
        WriteBitSet(buf, SkyYMask);
        WriteBitSet(buf, BlockYMask);
        WriteBitSet(buf, EmptySkyYMask);
        WriteBitSet(buf, EmptyBlockYMask);
        WriteDataLayers(buf, SkyUpdates);
        WriteDataLayers(buf, BlockUpdates);
    }

    //Read 从 FriendlyByteBuf 读取 ClientboundLightUpdatePacketData
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

    //WriteBitSet 写 BitSet 为 varint longs 数量 + longs
    //原版 writeBitSet 走 BitSet.toLongArray + writeLongArray 每个 long 是大端
    //内部 byte[] 按小端位布局存位 必须逐 long 转成大端 直接按字节顺序写会让位图错位到别的区段
    private static void WriteBitSet(FriendlyByteBuf buf, byte[] bytes)
    {
        var longCount = bytes.Length / 8;
        buf.WriteVarInt(longCount);
        for (var i = 0; i < longCount; i++)
            buf.WriteLong(ReadLittleEndianLong(bytes, i * 8));
    }

    //ReadBitSet 读 BitSet varint longs 数量 + longs
    private static byte[] ReadBitSet(FriendlyByteBuf buf)
    {
        var count = buf.ReadVarInt();
        var bytes = new byte[count * 8];
        for (var i = 0; i < count; i++)
            WriteLittleEndianLong(bytes, i * 8, buf.ReadLong());
        return bytes;
    }

    //ReadLittleEndianLong 把 8 字节按小端拼回 long
    private static long ReadLittleEndianLong(byte[] bytes, int offset)
    {
        long value = 0;
        for (var i = 0; i < 8; i++)
            value |= (long)bytes[offset + i] << (i * 8);
        return value;
    }

    //WriteLittleEndianLong 把 long 按小端拆回 8 字节
    private static void WriteLittleEndianLong(byte[] bytes, int offset, long value)
    {
        for (var i = 0; i < 8; i++)
            bytes[offset + i] = (byte)(value >> (i * 8));
    }

    //WriteDataLayers 写 DataLayer 列表 varint 数量 + 每项 varint 长度 + 2048 字节
    //原版 DATA_LAYER_STREAM_CODEC 是 ByteBufCodecs.byteArray(2048) 即 writeByteArray 带长度前缀
    private static void WriteDataLayers(FriendlyByteBuf buf, byte[][] layers)
    {
        buf.WriteVarInt(layers.Length);
        foreach (var layer in layers)
        {
            buf.WriteVarInt(layer.Length);
            buf.WriteBytes(layer);
        }
    }

    //ReadDataLayers 读 DataLayer 列表 varint 数量 + 每项 varint 长度 + 数据
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

    //LightDataBuild 两层光照构建结果
    private sealed record LightDataBuild(byte[] SkyMask, byte[] BlockMask, byte[] EmptySkyMask,
        byte[] EmptyBlockMask, byte[][] SkyUpdates, byte[][] BlockUpdates);

    //LayerBuild 单层构建结果
    private sealed record LayerBuild(byte[] Mask, byte[] EmptyMask, byte[][] Updates);

    //Prepare 按层从光照引擎取数据对应原版构造函数
    private static LightDataBuild Prepare(ChunkPos pos, LevelLightEngine lightEngine,
        byte[]? skyFilter, byte[]? blockFilter)
    {
        var sky = PrepareLayer(pos, lightEngine, LightLayer.Sky, skyFilter);
        var block = PrepareLayer(pos, lightEngine, LightLayer.Block, blockFilter);
        return new LightDataBuild(sky.Mask, block.Mask, sky.EmptyMask, block.EmptyMask, sky.Updates, block.Updates);
    }

    //PrepareLayer 逐光照区段取层数据 getDataLayerData 返回 null 表示该区段未启用不分发
    //有数据置 mask 并收集 2048 字节 空数据置 emptyMask 让客户端清空该区段
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
                //copy 后取字节避免下发期间引擎继续改同一块内存
                updates.Add(data.Copy().GetData());
            }
        }
        return new LayerBuild(TrimBitSet(mask), TrimBitSet(emptyMask), updates.ToArray());
    }

    //CreateFilter 按受影响区段序号构造过滤位图 空集合返回空位图表示该层没有变化
    public static byte[] CreateFilter(int sectionCount, IReadOnlyList<int> indices)
    {
        if (indices.Count == 0) return Array.Empty<byte>();
        var bits = new byte[(sectionCount + 63) / 64 * 8];
        foreach (var index in indices) SetBit(bits, index);
        return TrimBitSet(bits);
    }

    //IsSet 按区段序号读掩码位 供客户端还原各区段光照用
    public static bool IsSet(byte[] bits, int index)
        => index >= 0 && index / 8 < bits.Length
           && (bits[(index >> 6) * 8 + ((index >> 3) & 7)] & (1 << (index & 7))) != 0;

    //SetBit 按原版 BitSet 小端 long 布局置位
    private static void SetBit(byte[] bits, int index)
        => bits[(index >> 6) * 8 + ((index >> 3) & 7)] |= (byte)(1 << (index & 7));

    //TrimBitSet 去掉尾部全零 long 对齐原版 BitSet.toLongArray 的最短表示
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
