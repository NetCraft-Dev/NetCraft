using NetCraft.Codec;

namespace NetCraft.Util.Collection;

//固定大小校验工具对应原版net.minecraft.util.Util.fixedSize
//校验流或列表长度与预期size是否匹配不匹配返回DataResult.Error
public static class FixedSizeUtil
{
    //fixedSize校验int流长度对应原版Util.fixedSize(IntStream,int)
    //limit(size+1)多取一个用于区分刚好size与超过size的情况
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

    //fixedSize校验long流长度对应原版Util.fixedSize(LongStream,int)
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

    //fixedSize校验列表长度对应原版Util.fixedSize(List,int)
    //超size取前size段作为partial值短于size不返回partial
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
