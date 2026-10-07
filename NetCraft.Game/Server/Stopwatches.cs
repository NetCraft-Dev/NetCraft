using NetCraft.Registry;

namespace NetCraft.Game.Server;

//Stopwatches named stopwatch collection, maps to vanilla net.minecraft.world.Stopwatches
//The /stopwatch command creates, queries and restarts by id, used to time a piece of logic
public sealed class Stopwatches
{
    //_entries id to start milliseconds; only the zero moment is stored and the elapsed time is computed on query
    private readonly Dictionary<Identifier, long> _entries = new();

    //CurrentTime monotonically increasing milliseconds, maps to vanilla Stopwatches.currentTime
    public static long CurrentTime() => Environment.TickCount64;

    //Add creates a stopwatch; returns false when it already exists
    public bool Add(Identifier id, long startMillis) => _entries.TryAdd(id, startMillis);

    //Restart resets an existing stopwatch to the given moment; returns false when absent
    public bool Restart(Identifier id, long startMillis)
    {
        if (!_entries.ContainsKey(id)) return false;
        _entries[id] = startMillis;
        return true;
    }

    //GetStart gets the start moment; returns null when absent
    public long? GetStart(Identifier id) => _entries.TryGetValue(id, out var start) ? start : null;

    //Remove removes a stopwatch; returns false when absent
    public bool Remove(Identifier id) => _entries.Remove(id);
}
