using System.Collections.Generic;
using NetCraft.Nbt;

namespace NetCraft.Nbt.Visitors;

//收集指定字段的 StreamTagVisitor。对应原版 net.minecraft.nbt.visitors.CollectFields。
//仅构建被 FieldSelector 选中的字段（及其递归子树），其余字段跳过。
//收集完所有目标字段后会立即 BREAK（停止后续解析）。
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
        //始终需要CompoundTag类型才能递归进入子树
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

    //仍未收集到的目标字段数。</summary>
    public int GetMissingFieldCount() => _fieldsToGetCount;

    //条件删除：仅当 key 存在且 value 相等时删除。对应 Java Map.remove(key, value)。</summary>
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

