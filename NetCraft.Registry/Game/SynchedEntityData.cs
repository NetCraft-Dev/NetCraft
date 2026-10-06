namespace NetCraft.Registry;

//SynchedValue a single entity metadata entry, maps to vanilla SynchedEntityData.DataValue
//SerializerId is the Game layer EntityDataSerializers registry index; the Registry layer only passes it through without interpreting it
public readonly record struct SynchedValue(byte Index, int SerializerId, object Value);

//SynchedEntityData entity metadata container, maps to vanilla net.minecraft.network.syncher.SynchedEntityData
//Subclasses Define entries at construction, then Set changes values and Version increments
//The tracker records the version at the last send; a version mismatch resends all entries, avoiding separate incremental and full paths
public sealed class SynchedEntityData
{
    //Stored ordered by index; the send order matches vanilla's ascending index order
    private readonly SortedDictionary<byte, SynchedValue> _values = new();

    //Version change counter; observers compare it to tell whether anything changed
    public int Version { get; private set; }

    //Define declares an entry, maps to vanilla define
    public void Define(byte index, int serializerId, object value)
    {
        _values[index] = new SynchedValue(index, serializerId, value);
        Version++;
    }

    //Get returns the entry value; an undeclared index means a wrong index, so it throws instead of silently returning a wrong value
    public object Get(byte index)
        => _values.TryGetValue(index, out var value)
            ? value.Value
            : throw new InvalidOperationException($"Entity metadata {index} not defined");

    //Set changes the entry value; no change means no count increment, matching vanilla set marking dirty only on difference
    public void Set(byte index, object value)
    {
        if (!_values.TryGetValue(index, out var current))
            throw new InvalidOperationException($"Entity metadata {index} not defined");
        if (Equals(current.Value, value)) return;
        _values[index] = current with { Value = value };
        Version++;
    }

    //CollectAll returns all entries in ascending index order
    public List<SynchedValue> CollectAll()
    {
        var result = new List<SynchedValue>(_values.Count);
        foreach (var value in _values.Values) result.Add(value);
        return result;
    }
}

//ISyncedEntity an entity with synced metadata
//The tracker only knows this interface and no longer special-cases concrete types like ServerPlayer/ItemEntity
public interface ISyncedEntity
{
    //SyncedData entity metadata container
    SynchedEntityData SyncedData { get; }
}
