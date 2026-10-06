namespace NetCraft.Registry;

//TODO fill in Lifecycle during the DFU stage; vanilla has it as an abstract class from the DataFixer Upper library and it is simplified to an enum here
//Registry entry stability marker
public enum Lifecycle
{
    Stable = 0,
    Experimental = 1,
}

public static class LifecycleExtensions
{
    //Merge by keeping the less stable value
    public static Lifecycle Add(this Lifecycle a, Lifecycle b)
        => (Lifecycle)Math.Max((int)a, (int)b);
}
