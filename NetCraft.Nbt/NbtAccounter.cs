using NetCraft.Config;

namespace NetCraft.Nbt;

//NBT size accountant. Mirrors vanilla net.minecraft.nbt.NbtAccounter.
//Tracks bytes read and nesting depth so malicious saves cannot cause OOM or stack overflow.
public sealed class NbtAccounter
{
    private long _usage;
    private readonly long _quota;
    private int _depth;

    public NbtAccounter(long quota)
    {
        _quota = quota;
    }

    public NbtAccounter()
        : this(SharedConstants.MaxNbtAccounterBytes)
    {
    }

    //Bytes used so far.
    public long Usage => _usage;

    //Remaining quota.
    public long Quota => _quota;

    //Current nesting depth.
    public int Depth => _depth;

    //Consumes sizeInBytes of the quota. Throws NbtAccounterException when exceeded.
    public void AccountBytes(long sizeInBytes)
    {
        _usage += sizeInBytes;
        if (_usage > _quota)
        {
            throw new NbtAccounterException($"NBT accounter exceeded quota: tried to read {_usage} > {_quota} bytes");
        }
    }

    //Consumes overhead * count of the quota (for array elements).
    public void AccountBytes(long overhead, long count)
    {
        AccountBytes(overhead * count);
    }

    //Enter the next nesting level (guards against stack overflow from maliciously deep nesting).
    public void PushDepth()
    {
        if (_depth >= Tag.MaxDepth)
        {
            throw new NbtAccounterException($"NBT accounter exceeded max depth: {_depth} >= {Tag.MaxDepth}");
        }
        _depth++;
    }

    //Leave the current nesting level.
    public void PopDepth()
    {
        if (_depth == 0)
        {
            throw new NbtAccounterException("NBT accounter depth underflow: tried to pop depth when depth == 0");
        }
        _depth--;
    }

    //Unlimited instance (only for trusted data such as server internals).
    //Vanilla creates a new instance here rather than sharing a singleton: depth is mutable state, and when the server IOWorker reads chunks concurrently
    //sharing one instance would make Push/Pop trample each other, popping below depth 0 throws an underflow and chunk reads fail at random
    public static NbtAccounter UnlimitedHeap() => new(long.MaxValue);
}

//NBT size limit exception. Mirrors vanilla NbtAccounterException.
public sealed class NbtAccounterException(string message) : NbtException(message);

