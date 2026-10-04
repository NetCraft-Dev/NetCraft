namespace NetCraft.Primitives.Phys;

//IndexMerger 索引归并器 对应原版 IndexMerger
//把两条轴上的切分点归并成一条 布尔运算按归并出的格子逐块判定
public interface IIndexMerger
{
    //IndexConsumer 归并回调 返回 false 表示中断遍历 对应原版 IndexMerger.IndexConsumer
    //firstIndex 或 secondIndex 为 -1 表示该侧在这一格没有对应切分点
    public delegate bool IndexConsumer(int firstIndex, int secondIndex, int resultIndex);

    //List 归并后的坐标序列 对应原版 getList
    IReadOnlyList<double> List { get; }

    //ForMergedIndexes 逐格回调 对应原版 forMergedIndexes
    bool ForMergedIndexes(IndexConsumer consumer);

    //Size 归并后的格子数 比坐标点数少一 对应原版 size
    int Size { get; }
}
