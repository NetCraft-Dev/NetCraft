using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.DataFixer;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Storage;

//saveddata storage, maps to vanilla net.minecraft.world.level.storage.SavedDataStorage
//Manages reading, writing and upgrading of the SavedData entries under the saveddata directory
//readTagFromDisk goes through the real NbtIO + DataFixTypes.update upgrade path
//computeIfAbsent goes through the real ReadTagFromDisk + SavedDataType.Create path
//scheduleSave goes through the real dirty data scan + NbtIo.Write path
public sealed class SavedDataStorage : IDisposable
{
    private readonly NetCraft.DataFixer.DataFixer _fixerUpper;
    private readonly string _dataFolder;
    //cache indexes loaded SavedData instances by SavedDataType.Id
    private readonly Dictionary<string, SavedData> _cache = new();
    //registryAccess, the registry access entry point used by SavedDataType.Create for lookups
    private RegistryAccess? _registryAccess;
    private bool _closed;

    public SavedDataStorage(string dataFolder, NetCraft.DataFixer.DataFixer fixerUpper)
    {
        _fixerUpper = fixerUpper;
        _dataFolder = dataFolder;
    }

    //Set the RegistryAccess used by SavedDataType.Create in ComputeIfAbsent
    //Maps to vanilla savedDataStorage passing the registry entry point through HolderLookup.Provider
    public void SetRegistryAccess(RegistryAccess registryAccess) => _registryAccess = registryAccess;

    //readTagFromDisk reads a CompoundTag from disk and upgrades it by version through the DataFixer
    //Detects the gzip header automatically to choose ReadCompressed or Read
    //A missing dataVersion defaults to 1343, matching vanilla before the 1.13 snapshot
    //This project's DFU does not port the MC schema; when old-version data types are unregistered it warns and passes through without upgrading
    public CompoundTag ReadTagFromDisk(string dataFile, DataFixTypes type, int newVersion)
    {
        //Read the first two bytes to check for the gzip header 0x1f 0x8b
        using (var fs = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            int b1 = fs.ReadByte();
            int b2 = fs.ReadByte();
            bool isGzip = b1 == 0x1f && b2 == 0x8b;
            CompoundTag tag;
            if (isGzip)
            {
                tag = NbtIo.ReadCompressed(dataFile, NbtAccounter.UnlimitedHeap());
            }
            else
            {
                tag = NbtIo.Read(dataFile) ?? throw new IOException("Failed to read NBT from " + dataFile);
            }
            int version = NbtUtils.GetDataVersion(tag, 1343);
            if (version < newVersion)
            {
                try
                {
                    var dynamic = new Dynamic<Tag>(NbtOps.Instance, tag);
                    var fixedDynamic = type.Update(_fixerUpper, dynamic, version, newVersion);
                    tag = (CompoundTag)fixedDynamic.Value;
                }
                catch (ArgumentException e)
                {
                    Log.Warning($"Saved data {Path.GetFileName(dataFile)} upgrade skipped: {e.Message}");
                }
            }
            return tag;
        }
    }

    //getDataFile, the SavedData file path, aligns with vanilla Identifier.resolveAgainst
    //ns:path becomes ns/path.dat; without a namespace it goes as is under the data root
    private string GetDataFile(string id)
    {
        var idx = id.IndexOf(':');
        if (idx < 0) return Path.Combine(_dataFolder, id + ".dat");
        var ns = id[..idx];
        var path = id[(idx + 1)..] + ".dat";
        return Path.Combine(_dataFolder, ns, path);
    }

    //computeIfAbsent computes or gets existing data by SavedDataType, maps to vanilla computeIfAbsent
    //A cache hit returns the loaded instance; otherwise it reads from disk, creates via SavedDataType.Create and caches
    public T ComputeIfAbsent<T>(SavedDataType<T> type) where T : SavedData
    {
        if (_cache.TryGetValue(type.Id, out var cached)) return (T)cached;
        var dataFile = GetDataFile(type.Id);
        T data;
        if (File.Exists(dataFile))
        {
            //SavedData subclasses each have different DataFixTypes; Chunk is used as a placeholder to go through the generic DataFixer path
            var root = ReadTagFromDisk(dataFile, DataFixTypes.Chunk, SharedConstants.WorldDataVersion);
            //The payload sits under the data wrapper, maps to getCompoundOrEmpty("data") in vanilla readExistingSavedData
            var registryAccess = _registryAccess ?? new ImmutableRegistryAccess(Enumerable.Empty<KeyValuePair<Identifier, object>>());
            data = type.Create(root.GetCompoundOrEmpty("data"), registryAccess);
        }
        else
        {
            //A new instance goes through SavedDataType.Create with an empty CompoundTag placeholder, maps to vanilla SavedDataType.create
            var registryAccess = _registryAccess ?? new ImmutableRegistryAccess(Enumerable.Empty<KeyValuePair<Identifier, object>>());
            data = type.Create(new CompoundTag(), registryAccess);
        }
        _cache[type.Id] = data;
        return data;
    }

    //get takes the cached or disk-read data by SavedDataType
    public T? Get<T>(SavedDataType<T> type) where T : SavedData
        => _cache.TryGetValue(type.Id, out var data) ? (T)data : null;

    //GetOrLoad returns a cache hit directly, otherwise loads only if the file exists on disk, maps to vanilla SavedDataStorage.get
    //Command storage is sliced by namespace and must be readable from disk after a restart; pure cache semantics would read empty
    //Returns null when the file is missing, not creating an instance out of thin air like ComputeIfAbsent
    public T? GetOrLoad<T>(SavedDataType<T> type) where T : SavedData
    {
        if (_cache.TryGetValue(type.Id, out var cached)) return (T)cached;
        return File.Exists(GetDataFile(type.Id)) ? ComputeIfAbsent(type) : null;
    }

    //set caches the SavedData and marks it dirty
    public void Set<T>(SavedDataType<T> type, T data) where T : SavedData
    {
        _cache[type.Id] = data;
        data.SetDirty();
    }

    //scheduleSave collects dirty data and writes it to disk, maps to vanilla scheduleSave
    //The write includes DataVersion and a data wrapper; on read the version matches, so DFU short-circuits and no upgrade triggers
    //Returns a completed Task, aligning with vanilla CompletableFuture.allOf
    public Task ScheduleSave()
    {
        foreach (var (id, data) in _cache)
        {
            if (!data.IsDirty) continue;
            var dataFile = GetDataFile(id);
            Directory.CreateDirectory(Path.GetDirectoryName(dataFile)!);
            var fullTag = new CompoundTag();
            fullTag.Put("data", data.Save(new CompoundTag()));
            fullTag.PutInt("DataVersion", SharedConstants.WorldDataVersion);
            NbtIo.WriteCompressed(fullTag, dataFile);
            data.ClearDirty();
        }
        return Task.CompletedTask;
    }

    //saveAndJoin waits for scheduleSave to complete
    public void SaveAndJoin()
    {
        try
        {
            ScheduleSave().Wait();
        }
        catch (NotSupportedException)
        {
            //A no-op when there is no dirty data or the subsystem is not ready
        }
    }

    //close checks the closed state, calls saveAndJoin and then marks closed
    public void Dispose()
    {
        if (_closed)
        {
            throw new InvalidOperationException("Trying to close SavedDataStorage when it is already closed");
        }
        SaveAndJoin();
        _closed = true;
    }
}
