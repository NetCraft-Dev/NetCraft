namespace NetCraft.Primitives.Phys;

//IndirectMerger 通用索引归并 对应原版 IndirectMerger
//两侧切分点交错不规律时走这条 双指针扫描并按容差合并几乎重合的点
//firstOnlyMatters 为真表示只看第一侧落在格内的点 第二侧起辅助作用
public sealed class IndirectMerger : IIndexMerger
{
    //容差 两个切分点相差小于它视作同一个点 对应原版 1.0E-7
    private const double Tolerance = 1.0E-7;

    private static readonly double[] Empty = { 0.0 };

    private readonly double[] _result;
    private readonly int[] _firstIndices;
    private readonly int[] _secondIndices;
    private readonly int _resultLength;

    public IndirectMerger(IReadOnlyList<double> first, IReadOnlyList<double> second,
        bool firstOnlyMatters, bool secondOnlyMatters)
    {
        var lastValue = double.NaN;
        var firstSize = first.Count;
        var secondSize = second.Count;
        var capacity = firstSize + secondSize;
        _result = new double[capacity];
        _firstIndices = new int[capacity];
        _secondIndices = new int[capacity];
        var canSkipFirst = !firstOnlyMatters;
        var canSkipSecond = !secondOnlyMatters;
        var resultIndex = 0;
        var firstIndex = 0;
        var secondIndex = 0;
        while (true)
        {
            //这两个快照取循环开头 后面下标自增后判断仍用旧值 与原版一致
            var ranOutOfFirst = firstIndex >= firstSize;
            var ranOutOfSecond = secondIndex >= secondSize;
            if (ranOutOfFirst && ranOutOfSecond) break;
            var choseFirst = !ranOutOfFirst
                && (ranOutOfSecond || first[firstIndex] < second[secondIndex] + Tolerance);
            if (choseFirst)
            {
                firstIndex++;
                if (canSkipFirst && (secondIndex == 0 || ranOutOfSecond)) continue;
            }
            else
            {
                secondIndex++;
                if (canSkipSecond && (firstIndex == 0 || ranOutOfFirst)) continue;
            }

            var currentFirstIndex = firstIndex - 1;
            var currentSecondIndex = secondIndex - 1;
            var nextValue = choseFirst ? first[currentFirstIndex] : second[currentSecondIndex];
            if (!(lastValue >= nextValue - Tolerance))
            {
                _firstIndices[resultIndex] = currentFirstIndex;
                _secondIndices[resultIndex] = currentSecondIndex;
                _result[resultIndex] = nextValue;
                resultIndex++;
                lastValue = nextValue;
                continue;
            }

            //与上一个点重合 改写上一格的下标而不新增坐标
            _firstIndices[resultIndex - 1] = currentFirstIndex;
            _secondIndices[resultIndex - 1] = currentSecondIndex;
        }

        _resultLength = Math.Max(1, resultIndex);
    }

    public IReadOnlyList<double> List
        => _resultLength <= 1 ? Empty : new ArraySegment<double>(_result, 0, _resultLength);

    public int Size => _resultLength;

    public bool ForMergedIndexes(IIndexMerger.IndexConsumer consumer)
    {
        var length = _resultLength - 1;
        for (var i = 0; i < length; i++)
            if (!consumer(_firstIndices[i], _secondIndices[i], i)) return false;
        return true;
    }
}
