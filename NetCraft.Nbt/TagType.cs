namespace NetCraft.Nbt;

//NBT tag type description. Mirrors vanilla net.minecraft.nbt.TagType&lt;T&gt;.
//Reads, skips and stream-parses Tags of a specific type from binary data.
public interface TagType
{
    //Load one Tag instance from the input.
    Tag Load(INbtReader input, NbtAccounter accounter);

    //Stream parse (no full Tag object built, fed straight to the visitor).
    StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter);

    //Skip count Tags of this type.
    void Skip(INbtReader input, int count, NbtAccounter accounter);

    //Skip a single Tag of this type.
    void Skip(INbtReader input, NbtAccounter accounter);

    //Type name (such as "TAG_Byte").
    string Name { get; }

    //Readable name (such as "TAG_Byte()").
    string PrettyName { get; }

    //Parse as the root entry (mirrors vanilla parseRoot).
    void ParseRoot(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
    {
        switch (output.VisitRootEntry(this))
        {
            case StreamTagVisitor.ValueResult.Continue:
                Parse(input, output, accounter);
                break;
            case StreamTagVisitor.ValueResult.Break:
                Skip(input, accounter);
                break;
        }
    }

    //Fixed-size Tag types (byte/short/int/long/float/double).
    public interface StaticSize : TagType
    {
        //Bytes occupied by a single Tag.
        int Size { get; }

        void TagType.Skip(INbtReader input, NbtAccounter accounter) => input.SkipBytes(Size);

        void TagType.Skip(INbtReader input, int count, NbtAccounter accounter) => input.SkipBytes(Size * count);
    }

    //Variable-length Tag types (String/List/Compound/Array).
    public interface VariableSize : TagType
    {
        void TagType.Skip(INbtReader input, int count, NbtAccounter accounter)
        {
            for (var i = 0; i < count; i++)
            {
                Skip(input, accounter);
            }
        }
    }

    //Create an invalid TagType (for unknown Tag IDs).
    static TagType CreateInvalid(int id) => new InvalidTagType(id);
}

//Invalid Tag type (for unknown IDs).
internal sealed class InvalidTagType(int id) : TagType
{
    private IOException CreateException() => new($"Invalid tag id: {id}");

    public Tag Load(INbtReader input, NbtAccounter accounter) => throw CreateException();
    public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter) => throw CreateException();
    public void Skip(INbtReader input, int count, NbtAccounter accounter) => throw CreateException();
    public void Skip(INbtReader input, NbtAccounter accounter) => throw CreateException();
    public string Name => $"INVALID[{id}]";
    public string PrettyName => $"UNKNOWN_{id}";
}

