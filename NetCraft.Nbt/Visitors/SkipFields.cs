using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//StreamTagVisitor that skips the given fields. Mirrors vanilla net.minecraft.nbt.visitors.SkipFields.
//Skipped only when the field is selected by FieldSelector (VisitEntry returns Skip);
//other fields are built as Tags normally (through the base CollectToTag).
//Nested fields keep their stack frame through FieldTree.
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

