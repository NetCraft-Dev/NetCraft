using NetCraft.Primitives;

namespace NetCraft.Storage;

//SimulationChunkTracker, simulation level propagation, maps to vanilla net.minecraft.server.level.SimulationChunkTracker
//The source is the "simulation" ticket level; the result is kept in its own table and not written back to holders
public sealed class SimulationChunkTracker : ChunkTracker
{
    //MaxLevel, the level at which simulation does not apply, maps to vanilla MAX_LEVEL
    public const int MaxLevel = ChunkLevel.FullChunkLevel;

    //_chunks, map from chunk to simulation level; absent from the table means no simulation
    private readonly Dictionary<long, int> _chunks = new();
    private readonly TicketStorage _ticketStorage;

    public SimulationChunkTracker(TicketStorage ticketStorage)
        : base(MaxLevel + 1, 16, 256)
    {
        _ticketStorage = ticketStorage;
        ticketStorage.SetSimulationChunkUpdatedListener((node, level, onlyDecreased) => Update(node, level, onlyDecreased));
    }

    protected override int GetLevelFromSource(long packedPos)
        => _ticketStorage.GetTicketLevelAt(packedPos, true);

    protected override int GetLevel(long packedPos) => _chunks.GetValueOrDefault(packedPos, MaxLevel);

    protected override void SetLevel(long packedPos, int level)
    {
        if (level >= MaxLevel) _chunks.Remove(packedPos);
        else _chunks[packedPos] = level;
    }

    //GetLevel gets the simulation level by chunk pos, maps to vanilla getLevel(ChunkPos)
    public int GetLevel(ChunkPos pos) => GetLevel(pos.Pack());

    //GetLevelAt gets the simulation level by packed pos, maps to vanilla getLevel(long)
    //Chunks absent from the table return MaxLevel, i.e. "no simulation"; the caller uses that to decide whether entities and blocks can tick
    public int GetLevelAt(long packedPos) => GetLevel(packedPos);

    //RunAllUpdates keeps advancing to convergence, maps to vanilla runAllUpdates
    public void RunAllUpdates() => RunUpdates(int.MaxValue);
}
