namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Inj2注入Either右分支对应原版com.mojang.datafixers.optics.Inj2
//把Either<F,G>的右值G改写为G2构造Either<F,G2>左值不变
public sealed class Inj2<F, G, G2> : Prism<Either<F, G>, Either<F, G2>, G, G2>
{
    //单例缓存类型参数擦除后共享
    internal static readonly Inj2<object, object, object> Instance = new();

    private Inj2() { }

    //match右值返回Right<G>左值返回Left<Either<F,G2>>
    public Either<Either<F, G2>, G> Match(Either<F, G> either)
        => either.Map(f => Either<Either<F, G2>, G>.Left(Either<F, G2>.Left(f)), g => Either<Either<F, G2>, G>.Right(g));

    //build把G2放入Either右分支
    public Either<F, G2> Build(G2 g2) => Either<F, G2>.Right(g2);

    public override string ToString() => "inj2";
}
