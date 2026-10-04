using NetCraft.Config;

namespace NetCraft.Nbt;

//NBT 大小统计器。对应原版 net.minecraft.nbt.NbtAccounter。
//跟踪已读取的字节数与嵌套深度，防止恶意存档导致 OOM 或栈溢出。
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

    //当前已使用字节数。
    public long Usage => _usage;

    //剩余配额。
    public long Quota => _quota;

    //当前嵌套深度。
    public int Depth => _depth;

    //消耗 sizeInBytes 字节配额。超出抛 NbtAccounterException。
    public void AccountBytes(long sizeInBytes)
    {
        _usage += sizeInBytes;
        if (_usage > _quota)
        {
            throw new NbtAccounterException($"NBT accounter exceeded quota: tried to read {_usage} > {_quota} bytes");
        }
    }

    //消耗 overhead * count 字节配额（用于数组元素）。
    public void AccountBytes(long overhead, long count)
    {
        AccountBytes(overhead * count);
    }

    //进入下一层嵌套（防恶意深层嵌套导致栈溢出）。
    public void PushDepth()
    {
        if (_depth >= Tag.MaxDepth)
        {
            throw new NbtAccounterException($"NBT accounter exceeded max depth: {_depth} >= {Tag.MaxDepth}");
        }
        _depth++;
    }

    //退出当前层嵌套。
    public void PopDepth()
    {
        if (_depth == 0)
        {
            throw new NbtAccounterException("NBT accounter depth underflow: tried to pop depth when depth == 0");
        }
        _depth--;
    }

    //无限制实例（仅用于可信数据，如服务端内部）。
    //原版这里每次 new 而不是共享单例 depth 是可变状态 服务端 IOWorker 多线程并发读区块时
    //共享同一实例会让 Push/Pop 互相踩踏 pop 到 depth 0 以下抛下溢 区块读取随机失败
    public static NbtAccounter UnlimitedHeap() => new(long.MaxValue);
}

//NBT 大小超限异常。对应原版 NbtAccounterException。
public sealed class NbtAccounterException(string message) : NbtException(message);

