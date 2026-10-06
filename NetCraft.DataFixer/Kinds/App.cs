namespace NetCraft.DataFixer.Kinds;

//type application App<F,A> emulates the higher-kinded type F<A>
public interface App<F, A> where F : K1 { }
