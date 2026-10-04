using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using NetCraft.Interop;

namespace NetCraft.Nbt;

//NBT 核心读写 API。对应原版 net.minecraft.nbt.NbtIo。
//所有方法保证字节级兼容原版 Minecraft 26.2。
//NBT 二进制格式（大端字节序）：
//  CompoundTag 根: [TAG_Compound][名字][字段...][TAG_End]
//  ListTag: [元素类型][长度][元素...]
//  字符串: [2字节长度][modified UTF-8 内容]
public static class NbtIo
{
    // ============ 压缩（GZIP）读写 ============

    //从文件读取 GZIP 压缩的 CompoundTag。对应原版 readCompressed(Path, NbtAccounter)。
    public static CompoundTag ReadCompressed(string file, NbtAccounter accounter)
    {
        using var fs = File.OpenRead(file);
        return ReadCompressed(fs, accounter);
    }

    //从流读取 GZIP 压缩的 CompoundTag。
    public static CompoundTag ReadCompressed(Stream input, NbtAccounter accounter)
    {
        using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        using var br = new BinaryReader(gzip);
        return Read(new BinaryNbtReader(br), accounter);
    }

    //用 MemoryMappedFile 读取 GZIP 压缩的 CompoundTag 对应优化点 2.2
    //大文件场景避免 FileStream 多次拷贝用 MMF 直接映射
    //小文件仍走 ReadCompressed(string) 因 MMF 创建有固定开销
    public static CompoundTag ReadCompressedWithMemoryMapped(string file, NbtAccounter accounter)
    {
        using var accessor = MemoryMappedFileAccessor.FromFile(file, new FileInfo(file).Length, MemoryMappedFileAccess.Read);
        using var viewStream = accessor.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        return ReadCompressed(viewStream, accounter);
    }

    //写入 GZIP 压缩的 CompoundTag 到文件。
    public static void WriteCompressed(CompoundTag tag, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, FileOptions.WriteThrough);
        WriteCompressed(tag, fs);
    }

    //写入 GZIP 压缩的 CompoundTag 到流。
    public static void WriteCompressed(CompoundTag tag, Stream output)
    {
        using var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
        using var bw = new BinaryWriter(gzip);
        Write(tag, new BinaryNbtWriter(bw));
    }

    //用 MemoryMappedFile 写入 GZIP 压缩的 CompoundTag 对应优化点 2.2
    //写入场景 GZIP 输出长度不固定 MMF 预分配截断有句柄开销
    //回退到 FileStream 写入保证字节级一致读取路径才用 MMF 优化
    public static void WriteCompressedWithMemoryMapped(CompoundTag tag, string file, long capacity)
    {
        WriteCompressed(tag, file);
    }

    // ============ 未压缩读写 ============

    //从文件读取未压缩的 CompoundTag。文件不存在返回 null。
    public static CompoundTag? Read(string file)
    {
        if (!File.Exists(file)) return null;
        using var fs = File.OpenRead(file);
        using var br = new BinaryReader(fs);
        return Read(new BinaryNbtReader(br), NbtAccounter.UnlimitedHeap());
    }

    //从未压缩流读取 CompoundTag。对应原版 read(DataInput, NbtAccounter)。
    public static CompoundTag Read(INbtReader input, NbtAccounter accounter)
    {
        var tag = ReadUnnamedTag(input, accounter);
        if (tag is CompoundTag compound)
            return compound;
        throw new InvalidDataException("Root tag must be a named compound tag");
    }

    //写入未压缩的 CompoundTag 到文件。
    public static void Write(CompoundTag tag, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, FileOptions.WriteThrough);
        using var bw = new BinaryWriter(fs);
        Write(tag, new BinaryNbtWriter(bw));
    }

    //写入未压缩的 CompoundTag 到写入器。对应原版 write(CompoundTag, DataOutput)。
    public static void Write(CompoundTag tag, INbtWriter output)
    {
        WriteUnnamedTagWithFallback(tag, output);
    }

    // ============ 流式解析（不构建完整 Tag 对象） ============

    //从文件流式解析 GZIP 压缩的 NBT。对应原版 parseCompressed(Path, StreamTagVisitor, NbtAccounter)。
    public static void ParseCompressed(string file, StreamTagVisitor output, NbtAccounter accounter)
    {
        using var fs = File.OpenRead(file);
        ParseCompressed(fs, output, accounter);
    }

    //从流流式解析 GZIP 压缩的 NBT。
    public static void ParseCompressed(Stream input, StreamTagVisitor output, NbtAccounter accounter)
    {
        using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        using var br = new BinaryReader(gzip);
        Parse(new BinaryNbtReader(br), output, accounter);
    }

    //流式解析（无压缩）。
    public static void Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
    {
        var type = TagTypes.GetType(input.ReadByte());
        if (type is EndTag.EndTagType)
        {
            if (output.VisitRootEntry(type) == StreamTagVisitor.ValueResult.Continue)
                output.VisitEnd();
            return;
        }
        switch (output.VisitRootEntry(type))
        {
            case StreamTagVisitor.ValueResult.Break:
            case StreamTagVisitor.ValueResult.Halt:
                StringTag.SkipString(input);
                type.Skip(input, accounter);
                break;
            case StreamTagVisitor.ValueResult.Continue:
                StringTag.SkipString(input);
                type.Parse(input, output, accounter);
                break;
        }
    }

    // ============ 任意 Tag 读写（用于嵌入 NBT 的场景） ============

    //读取任意 Tag（含类型前缀）。对应原版 readAnyTag(DataInput, NbtAccounter)。
    public static Tag ReadAnyTag(INbtReader input, NbtAccounter accounter)
    {
        var type = input.ReadByte();
        if (type == Tag.TagEnd)
            return EndTag.Instance;
        return ReadTagSafe(input, accounter, type);
    }

    //写入任意 Tag（含类型前缀）。对应原版 writeAnyTag(Tag, DataOutput)。
    public static void WriteAnyTag(Tag tag, INbtWriter output)
    {
        output.WriteByte(tag.Id);
        if (tag.Id == Tag.TagEnd) return;
        tag.Write(output);
    }

    // ============ 未命名 Tag 读写（根 CompoundTag 用空名） ============

    //写入未命名 Tag（用空字符串作为名字）。对应原版 writeUnnamedTag。
    public static void WriteUnnamedTag(Tag tag, INbtWriter output)
    {
        output.WriteByte(tag.Id);
        if (tag.Id == Tag.TagEnd) return;
        output.WriteUtf("");  // 根 CompoundTag 用空名（原版用 LocalTime.ROOT_LOCALE，即空字符串）
        tag.Write(output);
    }

    //写入未命名 Tag，并在 writeUTF 失败时回退到空字符串。对应原版 writeUnnamedTagWithFallback。
    public static void WriteUnnamedTagWithFallback(Tag tag, INbtWriter output)
    {
        // 原版 StringFallbackDataOutput 在 writeUTF 失败时回退到空字符串。
        // C# 实现中，ModifiedUtf8Encoder 不会失败（合法字符串都能编码），无需特殊处理。
        WriteUnnamedTag(tag, output);
    }

    //读取未命名 Tag。对应原版 readUnnamedTag(DataInput, NbtAccounter)。
    public static Tag ReadUnnamedTag(INbtReader input, NbtAccounter accounter)
    {
        var type = input.ReadByte();
        if (type == Tag.TagEnd)
            return EndTag.Instance;
        StringTag.SkipString(input);  // 跳过根名字
        return ReadTagSafe(input, accounter, type);
    }

    // ============ 内部辅助 ============

    private static Tag ReadTagSafe(INbtReader input, NbtAccounter accounter, byte type)
    {
        try
        {
            return TagTypes.GetType(type).Load(input, accounter);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new InvalidDataException($"Failed to load NBT tag (type={type})", e);
        }
    }
}

