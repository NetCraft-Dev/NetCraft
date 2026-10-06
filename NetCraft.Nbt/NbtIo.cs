using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using NetCraft.Interop;

namespace NetCraft.Nbt;

//Core NBT read/write API. Mirrors vanilla net.minecraft.nbt.NbtIo.
//All methods are byte-compatible with vanilla Minecraft 26.2.
//NBT binary format (big-endian):
//  CompoundTag root: [TAG_Compound][name][fields...][TAG_End]
//  ListTag: [element type][length][elements...]
//  string: [2-byte length][modified UTF-8 payload]
public static class NbtIo
{
    // ============ compressed (GZIP) read/write ============

    //Read a GZIP-compressed CompoundTag from a file. Mirrors vanilla readCompressed(Path, NbtAccounter).
    public static CompoundTag ReadCompressed(string file, NbtAccounter accounter)
    {
        using var fs = File.OpenRead(file);
        return ReadCompressed(fs, accounter);
    }

    //Read a GZIP-compressed CompoundTag from a stream.
    public static CompoundTag ReadCompressed(Stream input, NbtAccounter accounter)
    {
        using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        using var br = new BinaryReader(gzip);
        return Read(new BinaryNbtReader(br), accounter);
    }

    //Read a GZIP-compressed CompoundTag via MemoryMappedFile, optimization 2.2
    //Avoid repeated FileStream copies on large files by mapping with an MMF
    //Small files still use ReadCompressed(string) since creating an MMF has a fixed cost
    public static CompoundTag ReadCompressedWithMemoryMapped(string file, NbtAccounter accounter)
    {
        using var accessor = MemoryMappedFileAccessor.FromFile(file, new FileInfo(file).Length, MemoryMappedFileAccess.Read);
        using var viewStream = accessor.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        return ReadCompressed(viewStream, accounter);
    }

    //Write a GZIP-compressed CompoundTag to a file.
    public static void WriteCompressed(CompoundTag tag, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, FileOptions.WriteThrough);
        WriteCompressed(tag, fs);
    }

    //Write a GZIP-compressed CompoundTag to a stream.
    public static void WriteCompressed(CompoundTag tag, Stream output)
    {
        using var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
        using var bw = new BinaryWriter(gzip);
        Write(tag, new BinaryNbtWriter(bw));
    }

    //Write a GZIP-compressed CompoundTag via MemoryMappedFile, optimization 2.2
    //GZIP output length is unknown when writing, so an MMF would need preallocation, truncation and handle overhead
    //Fall back to FileStream writes so bytes stay identical; only the read path uses the MMF optimization
    public static void WriteCompressedWithMemoryMapped(CompoundTag tag, string file, long capacity)
    {
        WriteCompressed(tag, file);
    }

    // ============ uncompressed read/write ============

    //Read an uncompressed CompoundTag from a file. Returns null when the file does not exist.
    public static CompoundTag? Read(string file)
    {
        if (!File.Exists(file)) return null;
        using var fs = File.OpenRead(file);
        using var br = new BinaryReader(fs);
        return Read(new BinaryNbtReader(br), NbtAccounter.UnlimitedHeap());
    }

    //Read a CompoundTag from an uncompressed stream. Mirrors vanilla read(DataInput, NbtAccounter).
    public static CompoundTag Read(INbtReader input, NbtAccounter accounter)
    {
        var tag = ReadUnnamedTag(input, accounter);
        if (tag is CompoundTag compound)
            return compound;
        throw new InvalidDataException("Root tag must be a named compound tag");
    }

    //Write an uncompressed CompoundTag to a file.
    public static void Write(CompoundTag tag, string file)
    {
        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, FileOptions.WriteThrough);
        using var bw = new BinaryWriter(fs);
        Write(tag, new BinaryNbtWriter(bw));
    }

    //Write an uncompressed CompoundTag to a writer. Mirrors vanilla write(CompoundTag, DataOutput).
    public static void Write(CompoundTag tag, INbtWriter output)
    {
        WriteUnnamedTagWithFallback(tag, output);
    }

    // ============ streaming parse (no full Tag object built) ============

    //Stream-parse GZIP-compressed NBT from a file. Mirrors vanilla parseCompressed(Path, StreamTagVisitor, NbtAccounter).
    public static void ParseCompressed(string file, StreamTagVisitor output, NbtAccounter accounter)
    {
        using var fs = File.OpenRead(file);
        ParseCompressed(fs, output, accounter);
    }

    //Stream-parse GZIP-compressed NBT from a stream.
    public static void ParseCompressed(Stream input, StreamTagVisitor output, NbtAccounter accounter)
    {
        using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        using var br = new BinaryReader(gzip);
        Parse(new BinaryNbtReader(br), output, accounter);
    }

    //Stream parse (uncompressed).
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

    // ============ any Tag read/write (for embedded NBT) ============

    //Read any Tag (with type prefix). Mirrors vanilla readAnyTag(DataInput, NbtAccounter).
    public static Tag ReadAnyTag(INbtReader input, NbtAccounter accounter)
    {
        var type = input.ReadByte();
        if (type == Tag.TagEnd)
            return EndTag.Instance;
        return ReadTagSafe(input, accounter, type);
    }

    //Write any Tag (with type prefix). Mirrors vanilla writeAnyTag(Tag, DataOutput).
    public static void WriteAnyTag(Tag tag, INbtWriter output)
    {
        output.WriteByte(tag.Id);
        if (tag.Id == Tag.TagEnd) return;
        tag.Write(output);
    }

    // ============ unnamed Tag read/write (root CompoundTag uses an empty name) ============

    //Write an unnamed Tag (empty string as the name). Mirrors vanilla writeUnnamedTag.
    public static void WriteUnnamedTag(Tag tag, INbtWriter output)
    {
        output.WriteByte(tag.Id);
        if (tag.Id == Tag.TagEnd) return;
        output.WriteUtf("");  // root CompoundTag uses an empty name (vanilla uses LocalTime.ROOT_LOCALE, i.e. an empty string)
        tag.Write(output);
    }

    //Write an unnamed Tag, falling back to an empty string when writeUTF fails. Mirrors vanilla writeUnnamedTagWithFallback.
    public static void WriteUnnamedTagWithFallback(Tag tag, INbtWriter output)
    {
        // Vanilla StringFallbackDataOutput falls back to an empty string when writeUTF fails.
        // In C# ModifiedUtf8Encoder never fails (any valid string can be encoded), so no special handling is needed.
        WriteUnnamedTag(tag, output);
    }

    //Read an unnamed Tag. Mirrors vanilla readUnnamedTag(DataInput, NbtAccounter).
    public static Tag ReadUnnamedTag(INbtReader input, NbtAccounter accounter)
    {
        var type = input.ReadByte();
        if (type == Tag.TagEnd)
            return EndTag.Instance;
        StringTag.SkipString(input);  // skip the root name
        return ReadTagSafe(input, accounter, type);
    }

    // ============ internal helpers ============

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

