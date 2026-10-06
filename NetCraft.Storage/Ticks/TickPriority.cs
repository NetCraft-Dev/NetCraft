namespace NetCraft.Storage.Ticks;

//TickPriority, scheduled tick priority, maps to vanilla net.minecraft.world.tick.TickPriority
//Lower values run first; within the same tick, priority is compared before sub-order
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
    //ByValue returns the priority for a value, clamping out-of-range to the ends, maps to vanilla TickPriority.byValue
    public static TickPriority ByValue(int value)
        => value <= (int)TickPriority.ExtremelyHigh
            ? TickPriority.ExtremelyHigh
            : value >= (int)TickPriority.ExtremelyLow
                ? TickPriority.ExtremelyLow
                : (TickPriority)value;
}
