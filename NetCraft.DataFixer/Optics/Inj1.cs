namespace NetCraft.DataFixer.Optics;

using NetCraft.DataFixer.Util;

//Inj1注入Either左分支对应原版com.mojang.datafixers.optics.Inj1
//把Either<F,G>的左值F改写为F2构造Either<F2,G>右值不变
public sealed class Inj1<F, G, F2> : Prism<Either<F, G>, Either<F2, G>, F, F2>
{
    //单例缓存类型参数擦除后共享
    internal static readonly Inj1<object, object, object> Instance = new();

    private Inj1() { }

    //match左值返回Right<F>右值返回Left<Either<F2,G>>
    //Map第一参数处理Left分支第二参数处理Right分支
    public Either<Either<F2, G>, F> Match(Either<F, G> either)
        => either.Map(f => Either<Either<F2, G>, F>.Right(f), g => Either<Either<F2, G>, F>.Left(Either<F2, G>.Right(g)));

    //build把F2放入Either左分支
    public Either<F2, G> Build(F2 f2) => Either<F2, G>.Left(f2);

    public override string ToString() => "inj1";
}
