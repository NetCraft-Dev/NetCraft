namespace NetCraft.Registry;

//TODO DFU阶段补全Lifecycle来自DataFixer Upper库原版是抽象类此处简化为枚举
//注册项稳定性标记
public enum Lifecycle
{
    Stable = 0,
    Experimental = 1,
}

public static class LifecycleExtensions
{
    //合并取更不稳定的
    public static Lifecycle Add(this Lifecycle a, Lifecycle b)
        => (Lifecycle)Math.Max((int)a, (int)b);
}
