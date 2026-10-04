using NetCraft.Util.Random;

namespace NetCraft.Util.Collection;

//随机集合工具对应原版net.minecraft.util.Util中随机相关集合方法
//移植GetRandom/GetRandomSafe/ToShuffledList/ShuffledCopy/Shuffle
public static class RandomCollections
{
    //getRandom按随机数选数组元素对应原版Util.getRandom(T[])
    //空数组抛IndexOutOfRange对齐原版ArrayIndexOutOfBounds
    public static T GetRandom<T>(IReadOnlyList<T> array, RandomSource random)
        => array[random.NextInt(array.Count)];

    //getRandom按随机数选int数组元素对应原版Util.getRandom(int[])
    public static int GetRandom(int[] array, RandomSource random)
        => array[random.NextInt(array.Length)];

    //getRandom按随机数选列表元素对应原版Util.getRandom(List)
    public static T GetRandom<T>(T[] array, RandomSource random)
        => array[random.NextInt(array.Length)];

    //getRandomSafe空列表返回None否则返回Some对应原版Util.getRandomSafe
    public static Option<T> GetRandomSafe<T>(IReadOnlyList<T> list, RandomSource random)
        => list.Count == 0 ? Option<T>.None() : Option<T>.Some(GetRandom(list, random));

    //shuffle原位洗牌对应原版Util.shuffle
    //Fisher-Yates反向遍历交换i-1与swapTo
    public static void Shuffle<T>(IList<T> list, RandomSource random)
    {
        var size = list.Count;
        for (var i = size; i > 1; i--)
        {
            var swapTo = random.NextInt(i);
            (list[i - 1], list[swapTo]) = (list[swapTo], list[i - 1]);
        }
    }

    //toShuffledList收集流到列表后洗牌对应原版Util.toShuffledList(Stream)
    public static List<T> ToShuffledList<T>(IEnumerable<T> source, RandomSource random)
    {
        var result = source.ToList();
        Shuffle(result, random);
        return result;
    }

    //toShuffledList收集int流到数组后洗牌对应原版Util.toShuffledList(IntStream)
    //重命名ToShuffledIntArray避免与泛型版ToShuffledList<int>重载冲突
    public static int[] ToShuffledIntArray(IEnumerable<int> source, RandomSource random)
    {
        var result = source.ToArray();
        var size = result.Length;
        for (var i = size; i > 1; i--)
        {
            var swapTo = random.NextInt(i);
            (result[i - 1], result[swapTo]) = (result[swapTo], result[i - 1]);
        }
        return result;
    }

    //shuffledCopy复制数组后洗牌对应原版Util.shuffledCopy(T[])
    public static List<T> ShuffledCopy<T>(T[] array, RandomSource random)
    {
        var copy = new List<T>(array);
        Shuffle(copy, random);
        return copy;
    }

    //shuffledCopy复制列表后洗牌对应原版Util.shuffledCopy(ObjectArrayList)
    public static List<T> ShuffledCopy<T>(IReadOnlyList<T> list, RandomSource random)
    {
        var copy = new List<T>(list);
        Shuffle(copy, random);
        return copy;
    }
}
