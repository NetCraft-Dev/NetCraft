using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//字段选择树。对应原版 net.minecraft.nbt.visitors.FieldTree（Record）。
//表示按深度组织的字段选择规则：SelectedFields 是当前深度要选的字段，
//FieldsToRecurse 是要递归进入的子树。
public sealed class FieldTree
{
    //当前深度（根为 1）。
    public int Depth { get; }

    //当前深度要保留的字段（名字 → 类型）。
    public Dictionary<string, TagType> SelectedFields { get; }

    //要递归进入的子树（名字 → 子 FieldTree）。
    public Dictionary<string, FieldTree> FieldsToRecurse { get; }

    private FieldTree(int depth)
    {
        Depth = depth;
        SelectedFields = new Dictionary<string, TagType>();
        FieldsToRecurse = new Dictionary<string, FieldTree>();
    }

    //创建根 FieldTree（depth=1）。
    public static FieldTree CreateRoot() => new(1);

    //添加一个字段选择规则。
    //若该字段的 path 长度 >= 当前深度，则递归进入对应子树；否则作为当前深度选中字段。
    public void AddEntry(FieldSelector field)
    {
        if (Depth <= field.Path.Count)
        {
            // 还有更深的路径，递归进入子树
            var key = field.Path[Depth - 1];
            if (!FieldsToRecurse.TryGetValue(key, out var child))
            {
                child = new FieldTree(Depth + 1);
                FieldsToRecurse[key] = child;
            }
            child.AddEntry(field);
        }
        else
        {
            // 路径已到底，作为当前选中字段
            SelectedFields[field.Name] = field.Type;
        }
    }

    //检查指定类型和名字的字段是否被选中（类型必须完全匹配）。
    public bool IsSelected(TagType type, string id)
        => SelectedFields.TryGetValue(id, out var t) && t == type;
}

