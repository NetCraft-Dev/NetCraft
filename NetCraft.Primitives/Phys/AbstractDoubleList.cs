using System.Collections;

namespace NetCraft.Primitives.Phys;

//AbstractDoubleList readonly double sequence base class, maps to vanilla AbstractDoubleList
//Subclasses only provide the indexer and Count, enumeration walks in index order
public abstract class AbstractDoubleList : IReadOnlyList<double>
{
    public abstract double this[int index] { get; }

    public abstract int Count { get; }

    public IEnumerator<double> GetEnumerator()
    {
        for (var i = 0; i < Count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
