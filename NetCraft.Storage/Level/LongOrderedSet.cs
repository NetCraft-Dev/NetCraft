namespace NetCraft.Storage;

//LongOrderedSet 保序去重的 long 集合对应原版 LongLinkedOpenHashSet 用到的部分
//只需要按插入顺序出队与 O(1) 去重 哈希表存节点加链表保序就够
internal sealed class LongOrderedSet
{
    private readonly Dictionary<long, LinkedListNode<long>> _nodes;
    private readonly LinkedList<long> _order = new();

    public LongOrderedSet(int minSize) => _nodes = new Dictionary<long, LinkedListNode<long>>(minSize);

    public bool IsEmpty => _order.Count == 0;

    //Add 不存在才追加到队尾返回是否新增
    public bool Add(long value)
    {
        if (_nodes.ContainsKey(value)) return false;
        _nodes[value] = _order.AddLast(value);
        return true;
    }

    //Remove 按值移除返回是否存在
    public bool Remove(long value)
    {
        if (!_nodes.Remove(value, out var node)) return false;
        _order.Remove(node);
        return true;
    }

    //RemoveFirst 取队首即最先进队的元素
    public long RemoveFirst()
    {
        var node = _order.First!;
        _order.RemoveFirst();
        _nodes.Remove(node.Value);
        return node.Value;
    }
}
