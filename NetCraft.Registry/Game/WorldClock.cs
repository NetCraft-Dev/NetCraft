namespace NetCraft.Registry;

//WorldClock world clock marker type, maps to vanilla net.minecraft.world.clock.WorldClock
//An empty record serving only as a carrier for a registry key; each dimension can be associated with a default clock
//Runtime state is maintained by the Game layer's ServerClockManager indexed by Holder
public sealed class WorldClock
{
}
