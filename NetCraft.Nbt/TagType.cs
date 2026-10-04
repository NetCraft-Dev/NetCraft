namespace NetCraft.Nbt;

//NBT 标签类型描述。对应原版 net.minecraft.nbt.TagType&lt;T&gt;。
//负责从二进制读取 / 跳过 / 流式解析特定类型的 Tag。
public interface TagType
{
    //从输入加载一个 Tag 实例。
    Tag Load(INbtReader input, NbtAccounter accounter);

    //流式解析（不构造完整 Tag 对象，直接喂给 visitor）。
    StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter);

    //跳过 count 个此类型的 Tag。
    void Skip(INbtReader input, int count, NbtAccounter accounter);

    //跳过单个此类型的 Tag。
    void Skip(INbtReader input, NbtAccounter accounter);

    //类型名（如 "TAG_Byte"）。
    string Name { get; }

    //易读名（如 "TAG_Byte()"）。
    string PrettyName { get; }

    //作为根条目解析（对应原版 parseRoot）。
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

    //固定大小的 Tag 类型（byte/short/int/long/float/double）。
    public interface StaticSize : TagType
    {
        //单个 Tag 占用的字节数。
        int Size { get; }

        void TagType.Skip(INbtReader input, NbtAccounter accounter) => input.SkipBytes(Size);

        void TagType.Skip(INbtReader input, int count, NbtAccounter accounter) => input.SkipBytes(Size * count);
    }

    //变长的 Tag 类型（String/List/Compound/Array）。
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

    //创建一个无效类型的 TagType（用于未知 Tag ID）。
    static TagType CreateInvalid(int id) => new InvalidTagType(id);
}

//无效 Tag 类型（用于未知 ID）。
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

