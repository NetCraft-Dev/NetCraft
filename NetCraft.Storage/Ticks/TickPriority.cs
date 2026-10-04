namespace NetCraft.Storage.Ticks;

//TickPriority 调度刻优先级 对应原版 net.minecraft.world.tick.TickPriority
//数值越小越先执行 同刻内先比优先级再比子序号
public enum TickPriority
{
    ExtremelyHigh = -3,
    VeryHigh = -2,
    High = -1,
    Normal = 0,
    Low = 1,
    VeryLow = 2,
    ExtremelyLow = 3,
}

public static class TickPriorities
{
    //ByValue 按数值取优先级 越界钳到两端 对应原版 TickPriority.byValue
    public static TickPriority ByValue(int value)
        => value <= (int)TickPriority.ExtremelyHigh
            ? TickPriority.ExtremelyHigh
            : value >= (int)TickPriority.ExtremelyLow
                ? TickPriority.ExtremelyLow
                : (TickPriority)value;
}
