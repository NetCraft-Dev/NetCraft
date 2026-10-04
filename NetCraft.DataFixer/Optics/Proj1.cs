namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Proj1投影Pair第一分量对应原版com.mojang.datafixers.optics.Proj1
//view取Pair.Firstupdate替换Pair.First保留Second
public sealed class Proj1<F, G, F2> : Lens<Pair<F, G>, Pair<F2, G>, F, F2>
{
    //单例缓存类型参数擦除后共享
    internal static readonly Proj1<object, object, object> Instance = new();

    private Proj1() { }

    public F View(Pair<F, G> pair) => pair.First;
    public Pair<F2, G> Update(F2 newValue, Pair<F, G> pair) => Pair<F2, G>.Of(newValue, pair.Second);

    public override string ToString() => "\u03C01";
}
