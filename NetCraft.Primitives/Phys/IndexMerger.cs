namespace NetCraft.Primitives.Phys;

//IndexMerger index merger, maps to vanilla IndexMerger
//Merges the split points of two axes into one, boolean operations decide cell by cell from the merged result
public interface IIndexMerger
{
    //IndexConsumer merge callback, returning false aborts the iteration, maps to vanilla IndexMerger.IndexConsumer
    //firstIndex or secondIndex of -1 means that side has no corresponding split point for this cell
    public delegate bool IndexConsumer(int firstIndex, int secondIndex, int resultIndex);

    //List the merged coordinate sequence, maps to vanilla getList
    IReadOnlyList<double> List { get; }

    //ForMergedIndexes callback per cell, maps to vanilla forMergedIndexes
    bool ForMergedIndexes(IndexConsumer consumer);

    //Size the number of merged cells, one less than the point count, maps to vanilla size
    int Size { get; }
}
