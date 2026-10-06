namespace NetCraft.Primitives.Phys;

//IndirectMerger general-purpose index merge, maps to vanilla IndirectMerger
//Used when the two sides' split points interleave irregularly, a two-pointer scan merges nearly coincident points within tolerance
//firstOnlyMatters true means only the first side's in-cell points count, the second side plays a supporting role
public sealed class IndirectMerger : IIndexMerger
{
    //Tolerance, two split points differing by less than this are treated as the same point, maps to vanilla 1.0E-7
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
            //These two snapshots are taken at the top of the loop, later checks still use the old values after the indices increment, same as vanilla
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

            //Coincides with the previous point, rewrites the previous cell's indices instead of adding a coordinate
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
