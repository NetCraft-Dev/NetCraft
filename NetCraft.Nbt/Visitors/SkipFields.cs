using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//跳过指定字段的 StreamTagVisitor。对应原版 net.minecraft.nbt.visitors.SkipFields。
//仅当字段被 FieldSelector 选中时跳过（VisitEntry 返回 Skip），
//其余字段正常构建为 Tag（通过基类 CollectToTag）。
//递归进入的字段通过 FieldTree 维护栈。
public class SkipFields : CollectToTag
{
    private readonly Stack<FieldTree> _stack = new();

    public SkipFields(params FieldSelector[] wantedFields)
    {
        var rootFrame = FieldTree.CreateRoot();
        foreach (var wantedField in wantedFields)
            rootFrame.AddEntry(wantedField);
        _stack.Push(rootFrame);
    }

    public override StreamTagVisitor.EntryResult VisitEntry(TagType type, string id)
    {
        var currentFrame = _stack.Peek();
        if (currentFrame.IsSelected(type, id))
            return StreamTagVisitor.EntryResult.Skip;
        if (type == CompoundTag.CompoundTagType.Instance
            && currentFrame.FieldsToRecurse.TryGetValue(id, out var newFrame))
        {
            _stack.Push(newFrame);
        }
        return base.VisitEntry(type, id);
    }

    public override StreamTagVisitor.ValueResult VisitContainerEnd()
    {
        if (Depth == _stack.Peek().Depth)
            _stack.Pop();
        return base.VisitContainerEnd();
    }
}

