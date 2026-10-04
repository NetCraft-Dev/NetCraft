using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//字段选择器。对应原版 net.minecraft.nbt.visitors.FieldSelector（Record）。
//描述要选择/保留的字段：路径 + 类型 + 名字。
//用于 SkipFields / CollectFields 指定要保留的子树。
public sealed record FieldSelector(IReadOnlyList<string> Path, TagType Type, string Name)
{
    //无路径构造（根字段）。
    public FieldSelector(TagType type, string name)
        : this(Array.Empty<string>(), type, name) { }

    //单层父路径。
    public FieldSelector(string parent, TagType type, string name)
        : this(new[] { parent }, type, name) { }

    //双层祖父-父路径。
    public FieldSelector(string grandparent, string parent, TagType type, string name)
        : this(new[] { grandparent, parent }, type, name) { }
}

