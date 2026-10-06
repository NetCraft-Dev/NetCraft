using NetCraft.Codec;

namespace NetCraft.Util.Collection;

//Fixed-size validation helper, maps to vanilla net.minecraft.util.Util.fixedSize
//Validates whether a stream or list length matches the expected size, returns DataResult.Error on mismatch
public static class FixedSizeUtil
{
    //fixedSize validates int stream length, maps to vanilla Util.fixedSize(IntStream,int)
    //limit(size+1) takes one extra to distinguish exactly size from exceeding size
    public static DataResult<int[]> FixedSize(IEnumerable<int> stream, int size)
    {
        var ints = stream.Take(size + 1).ToArray();
        if (ints.Length != size)
        {
            if (ints.Length >= size)
                return DataResult<int[]>.Error(() => $"Input is not a list of {size} ints", ints.Take(size).ToArray());
            return DataResult<int[]>.Error(() => $"Input is not a list of {size} ints");
        }
        return DataResult<int[]>.Success(ints);
    }

    //fixedSize validates long stream length, maps to vanilla Util.fixedSize(LongStream,int)
    public static DataResult<long[]> FixedSize(IEnumerable<long> stream, int size)
    {
        var longs = stream.Take(size + 1).ToArray();
        if (longs.Length != size)
        {
            if (longs.Length >= size)
                return DataResult<long[]>.Error(() => $"Input is not a list of {size} longs", longs.Take(size).ToArray());
            return DataResult<long[]>.Error(() => $"Input is not a list of {size} longs");
        }
        return DataResult<long[]>.Success(longs);
    }

    //fixedSize validates list length, maps to vanilla Util.fixedSize(List,int)
    //When exceeding size, takes the first size as the partial value; when shorter than size, returns no partial
    public static DataResult<IReadOnlyList<T>> FixedSize<T>(IReadOnlyList<T> list, int size)
    {
        if (list.Count != size)
        {
            if (list.Count >= size)
                return DataResult<IReadOnlyList<T>>.Error(() => $"Input is not a list of {size} elements", list.Take(size).ToList());
            return DataResult<IReadOnlyList<T>>.Error(() => $"Input is not a list of {size} elements");
        }
        return DataResult<IReadOnlyList<T>>.Success(list);
    }
}
