namespace NetCraft.DataFixer.Kinds;

//binary type constructor interface extending App, maps to vanilla Kind2 extends App
//Mu inherits K1 so a binary typeclass marker can serve as the Proof of Optic<Proof:K1>
//F is a K2 binary type constructor; App<Mu,F> wraps it unarily to reuse Kind1's App machinery
public interface Kind2<TF, TMu> : App<TMu, TF> where TF : K2 where TMu : IKind2Mu
{
    //binary typeclass marker inheriting K1 and IKind2Mu
    interface Mu : K1, IKind2Mu { }

    static Kind2<TF2, TMu2> Unbox<TF2, TMu2>(App<TMu2, TF2> proofBox) where TF2 : K2 where TMu2 : IKind2Mu
        => (Kind2<TF2, TMu2>)(object)proofBox;
}
