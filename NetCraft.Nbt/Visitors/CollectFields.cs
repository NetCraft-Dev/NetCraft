using System.Collections.Generic;
using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//StreamTagVisitor that collects the given fields. Mirrors vanilla net.minecraft.nbt.visitors.CollectFields.
//Only builds the fields selected by FieldSelector (and their recursive subtrees); other fields are skipped.
//BREAKs as soon as all target fields are collected (stopping the rest of the parse).
public class CollectFields : CollectToTag
{
    private int _fieldsToGetCount;
    private readonly HashSet<TagType> _wantedTypes;
    private readonly Stack<FieldTree> _stack = new();

    public CollectFields(params FieldSelector[] wantedFields)
    {
        _fieldsToGetCount = wantedFields.Length;
        _wantedTypes = new HashSet<TagType>();
        var rootFrame = FieldTree.CreateRoot();
        foreach (var wantedField in wantedFields)
        {
            rootFrame.AddEntry(wantedField);
            _wantedTypes.Add(wantedField.Type);
        }
        _stack.Push(rootFrame);
        //A CompoundTag type is always required to recurse into a subtree
        _wantedTypes.Add(CompoundTag.CompoundTagType.Instance);
    }

    public override StreamTagVisitor.ValueResult VisitRootEntry(TagType type)
    {
        if (type != CompoundTag.CompoundTagType.Instance)
            return StreamTagVisitor.ValueResult.Halt;
        return base.VisitRootEntry(type);
    }

    public override StreamTagVisitor.EntryResult VisitEntry(TagType type)
    {
        var currentFrame = _stack.Peek();
        if (Depth > currentFrame.Depth)
            return base.VisitEntry(type);
        if (_fieldsToGetCount <= 0)
            return StreamTagVisitor.EntryResult.Break;
        if (!_wantedTypes.Contains(type))
            return StreamTagVisitor.EntryResult.Skip;
        return base.VisitEntry(type);
    }

    public override StreamTagVisitor.EntryResult VisitEntry(TagType type, string id)
    {
        var currentFrame = _stack.Peek();
        if (Depth > currentFrame.Depth)
            return base.VisitEntry(type, id);
        if (RemoveSelected(currentFrame.SelectedFields, id, type))
        {
            _fieldsToGetCount--;
            return base.VisitEntry(type, id);
        }
        if (type == CompoundTag.CompoundTagType.Instance
            && currentFrame.FieldsToRecurse.TryGetValue(id, out var newFrame))
        {
            _stack.Push(newFrame);
            return base.VisitEntry(type, id);
        }
        return StreamTagVisitor.EntryResult.Skip;
    }

    public override StreamTagVisitor.ValueResult VisitContainerEnd()
    {
        if (Depth == _stack.Peek().Depth)
            _stack.Pop();
        return base.VisitContainerEnd();
    }

    //Number of target fields still not collected.</summary>
    public int GetMissingFieldCount() => _fieldsToGetCount;

    //Conditional removal: delete only when the key exists and the value is equal. Mirrors Java Map.remove(key, value).</summary>
    private static bool RemoveSelected(Dictionary<string, TagType> dict, string key, TagType value)
    {
        if (dict.TryGetValue(key, out var v) && ReferenceEquals(v, value))
        {
            dict.Remove(key);
            return true;
        }
        return false;
    }
}

