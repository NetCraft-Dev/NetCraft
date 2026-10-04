using System.IO;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Util;

namespace NetCraft.Storage;

//多RegionFile的LRU缓存管理对应原版RegionFileStorage
//按region坐标缓存256个RegionFile，提供chunk级别的CompoundTag读写
//优化点2.8：读取路径按开关RegionFileMemoryMapped选择MMF读取入口
public sealed class RegionFileStorage : IDisposable
{
    public const string AnvilExtension = ".mca";
    private const int MaxCacheSize = 256;

    private readonly RegionStorageInfo _info;
    private readonly string _folder;
    private readonly bool _sync;
    private readonly LinkedList<(long Key, RegionFile Region)> _lru = new();
    private readonly Dictionary<long, LinkedListNode<(long Key, RegionFile Region)>> _cache = new();
    //全方法互斥锁对应原版RegionFileStorage的synchronized
    //缺锁时LRU驱逐可能关掉正在写入的RegionFile引发ObjectDisposedException
    private readonly object _gate = new();

    public RegionFileStorage(RegionStorageInfo info, string folder, bool sync)
    {
        _info = info;
        _folder = folder;
        _sync = sync;
    }

    private RegionFile GetRegionFile(ChunkPos pos)
    {
        lock (_gate)
        {
            long key = ChunkPos.Pack(pos.GetRegionX(), pos.GetRegionZ());
            if (_cache.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Region;
            }
            FileUtil.CreateDirectoriesSafe(_folder);
            string file = Path.Combine(_folder, $"r.{pos.GetRegionX()}.{pos.GetRegionZ()}{AnvilExtension}");
            var region = new RegionFile(_info, file, _folder, _sync);
            var newNode = new LinkedListNode<(long Key, RegionFile Region)>((key, region));
            _lru.AddFirst(newNode);
            _cache[key] = newNode;
            if (_lru.Count > MaxCacheSize)
            {
                var last = _lru.Last!.Value;
                _lru.RemoveLast();
                _cache.Remove(last.Key);
                last.Region.Close();
            }
            return region;
        }
    }

    //按开关选择读取入口对应优化点2.8
    //MMF路径失败时降级到FileStream路径保证语义一致
    private static BinaryReader? OpenChunkInputStream(RegionFile region, ChunkPos pos)
    {
        if (OptimizationFlags.RegionFileMemoryMapped)
        {
            try
            {
                return region.GetChunkDataInputStreamWithMemoryMapped(pos);
            }
            catch (InvalidOperationException)
            {
                //文件过小走FileStream路径
                return region.GetChunkDataInputStream(pos);
            }
        }
        return region.GetChunkDataInputStream(pos);
    }

    public CompoundTag? Read(ChunkPos pos)
    {
        Log.Debug($"Read entry pos={pos}");
        lock (_gate)
        {
            var region = GetRegionFile(pos);
            var reader = OpenChunkInputStream(region, pos);
            if (reader == null)
            {
                //Log.Debug($"Read 出口 result=null");
                return null;
            }
            using (reader)
            {
                var result = NbtIo.Read(new BinaryNbtReader(reader), NbtAccounter.UnlimitedHeap());
                //Log.Debug($"Read 出口 result={result}");
                return result;
            }
        }
    }

    //按流式visitor扫描chunk，不构建完整Tag对象
    public void ScanChunk(ChunkPos pos, StreamTagVisitor scanner)
    {
        Log.Debug($"ScanChunk entry pos={pos}");
        lock (_gate)
        {
            var region = GetRegionFile(pos);
            var reader = OpenChunkInputStream(region, pos);
            if (reader == null)
            {
                //Log.Debug($"ScanChunk 出口");
                return;
            }
            using (reader)
                NbtIo.Parse(new BinaryNbtReader(reader), scanner, NbtAccounter.UnlimitedHeap());
        }
        //Log.Debug($"ScanChunk 出口");
    }

    public void Write(ChunkPos pos, CompoundTag? value)
    {
        //Log.Debug($"Write 入口 pos={pos} value={value}");
        Log.Debug($"Write entry pos={pos}");
        if (DebugFlags.DebugDontSaveWorld)
        {
            ////Log.Debug($"Write 出口");
            return;
        }
        //写入全程持锁 从GetChunkDataOutputStream到回写完成 驱逐关闭不可能插进中间
        lock (_gate)
        {
            var region = GetRegionFile(pos);
            if (value == null)
            {
                region.Clear(pos);
                //Log.Debug($"Write 出口");
                return;
            }
            var writer = region.GetChunkDataOutputStream(pos);
            using (writer)
                NbtIo.Write(value, new BinaryNbtWriter(writer));
        }
        //Log.Debug($"Write 出口");
    }

    public RegionStorageInfo Info() => _info;

    public void Flush()
    {
        //Log.Debug($"Flush 入口");
        lock (_gate)
        {
            foreach (var (_, region) in _lru)
                region.Flush();
        }
        //Log.Debug($"Flush 出口");
    }

    public void Close()
    {
        //Log.Debug($"Close 入口");
        var collector = new ExceptionCollector<IOException>();
        lock (_gate)
        {
            foreach (var (_, region) in _lru)
            {
                try { region.Close(); }
                catch (IOException e) { collector.Add(e); }
            }
            _lru.Clear();
            _cache.Clear();
        }
        collector.ThrowIfPresent();
        //Log.Debug($"Close 出口");
    }

    public void Dispose() => Close();
}
