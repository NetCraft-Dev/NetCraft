using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//TicketStorage, chunk ticket container, maps to vanilla net.minecraft.world.level.TicketStorage
//Active tickets drive loading and simulation; deactivated tickets only sit in memory waiting to be written; both kinds are written into chunk_tickets.dat
//On load tickets go into the deactivated area first and only become active at ActivateAllDeactivatedTickets, so old tickets do not pull chunks up before the world is ready
//Shutdown does the reverse to calm the world down quickly; the tickets stay in memory and are written to disk, then activated on the next startup
public sealed class TicketStorage : SavedData
{
    //TypeId, the save identifier, maps to the vanilla minecraft:chunk_tickets, written to data/minecraft/chunk_tickets.dat
    private const string TypeId = "minecraft:chunk_tickets";

    //TicketsTag, the ticket list field name, maps to the vanilla "tickets"
    private const string TicketsTag = "tickets";

    //ChunkPosTag, the packed chunk coord field of the ticket, maps to chunk_pos in the vanilla Pair
    private const string ChunkPosTag = "chunk_pos";
    //TypeTag, the ticket type registry name field, maps to the vanilla "type"
    private const string TypeTag = "type";
    //LevelTag, the ticket level field, maps to the vanilla "level"
    private const string LevelTag = "level";
    //TicksLeftTag, the remaining ticks field, maps to the vanilla "ticks_left"
    private const string TicksLeftTag = "ticks_left";

    //_tickets, active ticket table; the key is the packed ChunkPos and the value is the ticket list on that chunk
    private readonly Dictionary<long, List<Ticket>> _tickets = new();
    //_deactivatedTickets, deactivated ticket table; tickets stay here on load and shutdown
    private readonly Dictionary<long, List<Ticket>> _deactivatedTickets = new();
    //_chunksWithForcedTickets, the force-loaded chunk set, for /forceload query
    private readonly HashSet<long> _chunksWithForcedTickets = new();
    //_loadingListener, loading ticket level change callback, registered by LoadingChunkTracker
    private Action<long, int, bool>? _loadingListener;
    //_simulationListener, simulation ticket level change callback, registered by SimulationChunkTracker
    private Action<long, int, bool>? _simulationListener;

    //Type, the SavedData factory; on load all tickets go into the deactivated area
    public static readonly SavedDataType<TicketStorage> Type = new TicketStorageType(TypeId);

    //TypeFor returns the ticket table data type per dimension; the ticket table must be one per dimension
    //Sharing one across the three dimensions would overwrite the level callbacks on each new dimension, leaving only one receiving ticket changes
    //The overworld keeps the original identifier and other dimensions add a suffix, so they do not write into the same file
    public static SavedDataType<TicketStorage> TypeFor(Identifier dimension)
        => new TicketStorageType(dimension.Path == "overworld" ? TypeId : $"{TypeId}_{dimension.Path}");

    public override string Id => TypeId;

    public TicketStorage() { }

    //Constructor restoring from a save tag, maps to vanilla fromPacked
    public TicketStorage(CompoundTag tag) => Load(tag);

    private sealed class TicketStorageType(string id) : SavedDataType<TicketStorage>
    {
        public string Id => id;

        public TicketStorage Create(CompoundTag tag, RegistryAccess registryAccess) => new(tag);
    }

    //AddTicket adds a ticket; when the same type and level exists it is only renewed, not added again, maps to vanilla addTicket
    //Returns false when the same ticket existed and was only renewed
    public bool AddTicket(Ticket ticket, ChunkPos pos) => AddTicket(pos.Pack(), ticket);

    public bool AddTicket(long packed, Ticket ticket)
    {
        var list = GetOrCreateList(_tickets, packed);
        foreach (var existing in list)
        {
            if (!existing.IsSameTypeAndLevel(ticket)) continue;
            existing.ResetTicksLeft();
            SetDirty();
            return false;
        }
        var oldSimulationLevel = GetTicketLevelIn(list, true);
        var oldLoadingLevel = GetTicketLevelIn(list, false);
        list.Add(ticket);
        //Notify only when the level got smaller; level increases are handled by the removal side
        if (ticket.Type.DoesSimulate && ticket.Level < oldSimulationLevel)
            _simulationListener?.Invoke(packed, ticket.Level, true);
        if (ticket.Type.DoesLoad && ticket.Level < oldLoadingLevel)
            _loadingListener?.Invoke(packed, ticket.Level, true);
        if (ReferenceEquals(ticket.Type, TicketType.Forced)) _chunksWithForcedTickets.Add(packed);
        SetDirty();
        return true;
    }

    //RemoveTicket removes the ticket of the same type and level, maps to vanilla removeTicket
    public bool RemoveTicket(TicketType type, int level, ChunkPos pos)
        => RemoveTicket(new Ticket(type, level), pos);

    public bool RemoveTicket(Ticket ticket, ChunkPos pos) => RemoveTicket(pos.Pack(), ticket);

    public bool RemoveTicket(long packed, Ticket ticket)
    {
        if (!_tickets.TryGetValue(packed, out var list)) return false;
        var found = false;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].IsSameTypeAndLevel(ticket)) continue;
            list.RemoveAt(i);
            found = true;
        }
        if (!found) return false;
        if (list.Count == 0) _tickets.Remove(packed);
        //After removal, re-report the level based on the remaining tickets; this side does not carry onlyDecreased
        if (ticket.Type.DoesSimulate) _simulationListener?.Invoke(packed, GetTicketLevelIn(list, true), false);
        if (ticket.Type.DoesLoad) _loadingListener?.Invoke(packed, GetTicketLevelIn(list, false), false);
        if (ReferenceEquals(ticket.Type, TicketType.Forced)) UpdateForcedChunks();
        SetDirty();
        return true;
    }

    //AddTicketWithRadius issues a ticket by radius, level = 33 - radius, maps to vanilla addTicketWithRadius
    public void AddTicketWithRadius(TicketType type, ChunkPos pos, int radius)
        => AddTicket(new Ticket(type, ChunkLevel.FullChunkLevel - radius), pos);

    //RemoveTicketWithRadius removes a ticket by radius, maps to vanilla removeTicketWithRadius
    public bool RemoveTicketWithRadius(TicketType type, ChunkPos pos, int radius)
        => RemoveTicket(type, ChunkLevel.FullChunkLevel - radius, pos);

    //GetTicketLevelAt, the chunk's current ticket level; no tickets returns MaxLevel+1 meaning not loaded, maps to vanilla getTicketLevelAt
    //When simulation is true only simulation tickets count, when false only loading tickets
    public int GetTicketLevelAt(long packedPos, bool simulation)
        => GetTicketLevelIn(_tickets.GetValueOrDefault(packedPos), simulation);

    //GetTicketLevelIn, the lowest level in the ticket list considering only tickets applicable to that use; an empty list returns MaxLevel+1
    private static int GetTicketLevelIn(List<Ticket>? list, bool simulation)
    {
        if (list is null) return ChunkLevel.MaxLevel + 1;
        var level = ChunkLevel.MaxLevel + 1;
        foreach (var ticket in list)
        {
            var applies = simulation ? ticket.Type.DoesSimulate : ticket.Type.DoesLoad;
            if (applies && ticket.Level < level) level = ticket.Level;
        }
        return level;
    }

    //GetTickets, the active tickets on that chunk, or null when none, maps to vanilla getTickets
    public IReadOnlyList<Ticket>? GetTickets(long packedPos) => _tickets.GetValueOrDefault(packedPos);

    //UpdateChunkForced toggles force load, maps to vanilla updateChunkForced
    //The level is the entity-ticking tier, matching vanilla ChunkMap.FORCED_TICKET_LEVEL
    public bool UpdateChunkForced(ChunkPos pos, bool add)
        => add
            ? AddTicket(new Ticket(TicketType.Forced, ChunkLevel.EntityTickingLevel), pos)
            : RemoveTicket(TicketType.Forced, ChunkLevel.EntityTickingLevel, pos);

    //GetForceLoadedChunks, packed coords of currently force-loaded chunks, maps to vanilla getForceLoadedChunks
    public IReadOnlyCollection<long> GetForceLoadedChunks() => _chunksWithForcedTickets;

    //SetLoadingChunkUpdatedListener registers the loading ticket change callback, maps to vanilla setLoadingChunkUpdatedListener
    public void SetLoadingChunkUpdatedListener(Action<long, int, bool> listener) => _loadingListener = listener;

    //SetSimulationChunkUpdatedListener registers the simulation ticket change callback, maps to vanilla setSimulationChunkUpdatedListener
    public void SetSimulationChunkUpdatedListener(Action<long, int, bool> listener) => _simulationListener = listener;

    //UpdateForcedChunks recomputes the force-loaded set, maps to vanilla updateForcedChunks
    private void UpdateForcedChunks()
    {
        _chunksWithForcedTickets.Clear();
        foreach (var (packed, tickets) in _tickets)
            foreach (var ticket in tickets)
            {
                if (!ReferenceEquals(ticket.Type, TicketType.Forced)) continue;
                _chunksWithForcedTickets.Add(packed);
                break;
            }
    }

    //ShouldKeepDimensionActive, whether any ticket requires the dimension to stay active, maps to vanilla shouldKeepDimensionActive
    public bool ShouldKeepDimensionActive()
    {
        foreach (var list in _tickets.Values)
            foreach (var ticket in list)
                if (ticket.Type.ShouldKeepDimensionActive) return true;
        return false;
    }

    //PurgeStaleTickets clears timed-out tickets once per tick, maps to vanilla purgeStaleTickets
    //The isReadyForSaving callback decides whether a chunk can safely drop tickets; a false keeps them this tick
    //Tickets with CanExpireIfUnloaded are not gated by the callback and clear even if the chunk is not ready
    public void PurgeStaleTickets(Func<long, bool>? isReadyForSaving = null)
    {
        List<(long Packed, Ticket Ticket)>? expired = null;
        foreach (var (packed, list) in _tickets)
        {
            foreach (var ticket in list)
            {
                if (!ticket.Type.HasTimeout) continue;
                if (!ticket.Type.CanExpireIfUnloaded && isReadyForSaving is not null
                    && !isReadyForSaving(packed)) continue;
                ticket.DecreaseTicksLeft();
                if (ticket.IsTimedOut) (expired ??= new()).Add((packed, ticket));
            }
        }
        if (expired is null) return;
        foreach (var (packed, ticket) in expired)
            RemoveTicket(ticket.Type, ticket.Level, ChunkPos.Unpack(packed));
    }

    //ActivateAllDeactivatedTickets turns all deactivated tickets active, maps to vanilla activateAllDeactivatedTickets
    //The startup chain calls it after the initial chunks are prepared; only then is the world ready for old tickets to drive loading
    public void ActivateAllDeactivatedTickets()
    {
        if (_deactivatedTickets.Count == 0) return;
        foreach (var (packed, list) in _deactivatedTickets)
        {
            var pos = ChunkPos.Unpack(packed);
            foreach (var ticket in list) AddTicket(ticket, pos);
        }
        _deactivatedTickets.Clear();
    }

    //DeactivateTicketsOnClosing moves active tickets into the deactivated area on shutdown, maps to vanilla deactivateTicketsOnClosing
    //After deactivation they no longer drive loading, but they stay in memory and are written to disk, then activated on the next startup
    //UNKNOWN is a temporary ticket and is not moved
    public void DeactivateTicketsOnClosing()
    {
        foreach (var (packed, list) in _tickets)
            foreach (var ticket in list)
            {
                if (ReferenceEquals(ticket.Type, TicketType.Unknown)) continue;
                GetOrCreateList(_deactivatedTickets, packed).Add(ticket);
            }
        _tickets.Clear();
        _chunksWithForcedTickets.Clear();
    }

    //Save writes only persist tickets, including deactivated ones, maps to the filter in vanilla packTickets
    public override CompoundTag Save(CompoundTag tag)
    {
        var list = new ListTag();
        PackInto(_tickets, list);
        PackInto(_deactivatedTickets, list);
        tag.Put(TicketsTag, list);
        return tag;
    }

    //Load puts all disk tickets into the deactivated area first, taking effect only at ActivateAllDeactivatedTickets, maps to vanilla fromPacked
    private void Load(CompoundTag tag)
    {
        var list = tag.GetListOrEmpty(TicketsTag);
        foreach (var element in list)
        {
            if (element is not CompoundTag entry) continue;
            var typeName = entry.GetStringValue(TypeTag);
            var type = Identifier.TryParse(typeName) is { } identifier
                ? TicketType.ByName(identifier)
                : null;
            if (type is null)
            {
                Log.Warning($"Unknown ticket type {typeName}");
                continue;
            }
            GetOrCreateList(_deactivatedTickets, entry.GetLongOr(ChunkPosTag, 0L))
                .Add(new Ticket(type, entry.GetIntOr(LevelTag, 0), entry.GetLongOr(TicksLeftTag, 0L)));
        }
    }

    //PackInto writes each persist ticket in the table into the list
    private static void PackInto(Dictionary<long, List<Ticket>> map, ListTag list)
    {
        foreach (var (packed, tickets) in map)
            foreach (var ticket in tickets)
            {
                if (!ticket.Type.Persist) continue;
                var entry = new CompoundTag();
                entry.PutLong(ChunkPosTag, packed);
                entry.PutString(TypeTag, ticket.Type.Name.ToString());
                entry.PutInt(LevelTag, ticket.Level);
                entry.PutLong(TicksLeftTag, ticket.TicksLeft);
                list.Add(entry);
            }
    }

    //GetOrCreateList gets or creates the ticket list of a chunk
    private static List<Ticket> GetOrCreateList(Dictionary<long, List<Ticket>> map, long packed)
    {
        if (map.TryGetValue(packed, out var list)) return list;
        list = new List<Ticket>();
        map[packed] = list;
        return list;
    }
}
