namespace NetCraft.DataFixer.Kinds;

//binary type application App2<F,A,B> emulates the higher-kinded type F<A,B>
public interface App2<F, A, B> where F : K2 { }
