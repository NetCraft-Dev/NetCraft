namespace NetCraft.Storage;

//LongOrderedSet, order-preserving deduplicating long set, covering the part of vanilla LongLinkedOpenHashSet that is used
//Only insertion-order dequeue and O(1) dedup are needed; a hash map of nodes plus a linked list for order suffices
internal sealed class LongOrderedSet
{
    private readonly Dictionary<long, LinkedListNode<long>> _nodes;
    private readonly LinkedList<long> _order = new();

    public LongOrderedSet(int minSize) => _nodes = new Dictionary<long, LinkedListNode<long>>(minSize);

    public bool IsEmpty => _order.Count == 0;

    //Add appends to the tail only when absent, returns whether it was added
    public bool Add(long value)
    {
        if (_nodes.ContainsKey(value)) return false;
        _nodes[value] = _order.AddLast(value);
        return true;
    }

    //Remove removes by value, returns whether it existed
    public bool Remove(long value)
    {
        if (!_nodes.Remove(value, out var node)) return false;
        _order.Remove(node);
        return true;
    }

    //RemoveFirst takes the head, i.e. the earliest enqueued element
    public long RemoveFirst()
    {
        var node = _order.First!;
        _order.RemoveFirst();
        _nodes.Remove(node.Value);
        return node.Value;
    }
}
