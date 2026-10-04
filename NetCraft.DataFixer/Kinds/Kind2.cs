namespace NetCraft.DataFixer.Kinds;

//二元类型构造器接口继承App对应原版Kind2 extends App
//Mu继承K1使二元类型类标记可作为Optic<Proof:K1>的Proof
//F是K2二元类型构造器通过App<Mu,F>一元包装复用Kind1的App机制
public interface Kind2<TF, TMu> : App<TMu, TF> where TF : K2 where TMu : IKind2Mu
{
    //二元类型类标记继承K1与IKind2Mu
    interface Mu : K1, IKind2Mu { }

    static Kind2<TF2, TMu2> Unbox<TF2, TMu2>(App<TMu2, TF2> proofBox) where TF2 : K2 where TMu2 : IKind2Mu
        => (Kind2<TF2, TMu2>)(object)proofBox;
}
