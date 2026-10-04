namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Proj2投影Pair第二分量对应原版com.mojang.datafixers.optics.Proj2
//view取Pair.Secondupdate替换Pair.Second保留First
public sealed class Proj2<F, G, G2> : Lens<Pair<F, G>, Pair<F, G2>, G, G2>
{
    //单例缓存类型参数擦除后共享
    internal static readonly Proj2<object, object, object> Instance = new();

    private Proj2() { }

    public G View(Pair<F, G> pair) => pair.Second;
    public Pair<F, G2> Update(G2 newValue, Pair<F, G> pair) => Pair<F, G2>.Of(pair.First, newValue);

    public override string ToString() => "\u03C02";
}
