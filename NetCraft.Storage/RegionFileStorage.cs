using System.IO;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Util;

namespace NetCraft.Storage;

//LRU cache management for multiple RegionFiles, maps to vanilla RegionFileStorage
//Caches up to 256 RegionFiles by region coords, providing chunk-level CompoundTag read/write
//Optimization 2.8: the read path selects the MMF read entry point based on the RegionFileMemoryMapped switch
public sealed class RegionFileStorage : IDisposable
{
    public const string AnvilExtension = ".mca";
    private const int MaxCacheSize = 256;

    private readonly RegionStorageInfo _info;
    private readonly string _folder;
    private readonly bool _sync;
    private readonly LinkedList<(long Key, RegionFile Region)> _lru = new();
    private readonly Dictionary<long, LinkedListNode<(long Key, RegionFile Region)>> _cache = new();
    //A whole-method mutex, maps to the synchronized in vanilla RegionFileStorage
    //Without the lock, LRU eviction could close a RegionFile being written, causing ObjectDisposedException
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

    //Select the read entry point by switch, optimization 2.8
    //Falls back to the FileStream path when MMF fails, keeping semantics identical
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
                //File too small, use the FileStream path
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
                //Log.Debug($"Read exit result=null");
                return null;
            }
            using (reader)
            {
                var result = NbtIo.Read(new BinaryNbtReader(reader), NbtAccounter.UnlimitedHeap());
                //Log.Debug($"Read exit result={result}");
                return result;
            }
        }
    }

    //Scan a chunk through a streaming visitor without building a full Tag
    public void ScanChunk(ChunkPos pos, StreamTagVisitor scanner)
    {
        Log.Debug($"ScanChunk entry pos={pos}");
        lock (_gate)
        {
            var region = GetRegionFile(pos);
            var reader = OpenChunkInputStream(region, pos);
            if (reader == null)
            {
                //Log.Debug($"ScanChunk exit");
                return;
            }
            using (reader)
                NbtIo.Parse(new BinaryNbtReader(reader), scanner, NbtAccounter.UnlimitedHeap());
        }
        //Log.Debug($"ScanChunk exit");
    }

    public void Write(ChunkPos pos, CompoundTag? value)
    {
        //Log.Debug($"Write entry pos={pos} value={value}");
        Log.Debug($"Write entry pos={pos}");
        if (DebugFlags.DebugDontSaveWorld)
        {
            ////Log.Debug($"Write exit");
            return;
        }
        //The lock is held for the whole write, from GetChunkDataOutputStream to write-back completion; an eviction close cannot slip in
        lock (_gate)
        {
            var region = GetRegionFile(pos);
            if (value == null)
            {
                region.Clear(pos);
                //Log.Debug($"Write exit");
                return;
            }
            var writer = region.GetChunkDataOutputStream(pos);
            using (writer)
                NbtIo.Write(value, new BinaryNbtWriter(writer));
        }
        //Log.Debug($"Write exit");
    }

    public RegionStorageInfo Info() => _info;

    public void Flush()
    {
        //Log.Debug($"Flush entry");
        lock (_gate)
        {
            foreach (var (_, region) in _lru)
                region.Flush();
        }
        //Log.Debug($"Flush exit");
    }

    public void Close()
    {
        //Log.Debug($"Close entry");
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
        //Log.Debug($"Close exit");
    }

    public void Dispose() => Close();
}
