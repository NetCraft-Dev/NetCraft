namespace NetCraft.Registry;

//ClockManager clock query interface, maps to vanilla net.minecraft.world.clock.ClockManager
//Timeline uses this interface to get a clock's total ticks; the server-side implementation is the Game layer's ServerClockManager
public interface ClockManager
{
    //GetTotalTicks gets the accumulated total ticks of the given clock
    long GetTotalTicks(Holder<WorldClock> definition);
}
