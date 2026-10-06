using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//Field selector. Mirrors vanilla net.minecraft.nbt.visitors.FieldSelector (a Record).
//Describes a field to select/keep: path + type + name.
//Used by SkipFields and CollectFields to specify the subtree to keep.
public sealed record FieldSelector(IReadOnlyList<string> Path, TagType Type, string Name)
{
    //Constructor with no path (a root field).
    public FieldSelector(TagType type, string name)
        : this(Array.Empty<string>(), type, name) { }

    //Single-level parent path.
    public FieldSelector(string parent, TagType type, string name)
        : this(new[] { parent }, type, name) { }

    //Two-level grandparent-parent path.
    public FieldSelector(string grandparent, string parent, TagType type, string name)
        : this(new[] { grandparent, parent }, type, name) { }
}

