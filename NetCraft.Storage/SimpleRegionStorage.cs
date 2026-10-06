using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.DataFixer;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Util;

namespace NetCraft.Storage;

//Generic region storage, maps to vanilla SimpleRegionStorage
//Holds an IOWorker wrapping async IO, exposing high-level APIs read/write/synchronize/chunkScanner
//Holds DataFixer and DataFixTypes to run the real upgradeChunkTag path
//Errors are wrapped as ReportedException to align with the vanilla error report path
public sealed class SimpleRegionStorage : IDisposable
{
    //Data-fix context tag name, maps to vanilla ChunkHeightAndBiomeFix.DATAFIXER_CONTEXT_TAG
    public const string DatafixerContextTag = "__context";
    private readonly IOWorker _worker;
    private readonly NetCraft.DataFixer.DataFixer _fixerUpper;
    private readonly DataFixTypes _dataFixType;

    public SimpleRegionStorage(RegionStorageInfo info, string folder, NetCraft.DataFixer.DataFixer fixerUpper, bool syncWrites, DataFixTypes dataFixType)
    {
        _worker = new IOWorker(info, folder, syncWrites);
        _fixerUpper = fixerUpper;
        _dataFixType = dataFixType;
    }

    public bool IsOldChunkAround(ChunkPos pos, int range) => _worker.IsOldChunkAround(pos, range);

    public Task<Optional<CompoundTag>> Read(ChunkPos pos) => _worker.LoadAsync(pos);

    public Task Write(ChunkPos pos, CompoundTag value) => _worker.Store(pos, value);

    public Task Write(ChunkPos pos, Func<CompoundTag> supplier) => _worker.Store(pos, supplier);

    //Upgrade chunkTag to targetVersion, maps to vanilla upgradeChunkTag
    //Delegates to DataFixTypes.update through the real DataFixer.update path; exceptions are wrapped as ReportedException
    public CompoundTag UpgradeChunkTag(CompoundTag chunkTag, int defaultVersion, CompoundTag? dataFixContextTag, int targetVersion)
    {
        int version = NbtUtils.GetDataVersion(chunkTag, defaultVersion);
        if (version >= targetVersion) return chunkTag;
        try
        {
            InjectDatafixingContext(chunkTag, dataFixContextTag);
            var dynamic = new Dynamic<Tag>(NbtOps.Instance, chunkTag);
            var fixedDynamic = _dataFixType.Update(_fixerUpper, dynamic, version, targetVersion);
            var fixedTag = (CompoundTag)fixedDynamic.Value;
            RemoveDatafixingContext(fixedTag);
            NbtUtils.AddDataVersion(fixedTag, targetVersion);
            return fixedTag;
        }
        catch (Exception e)
        {
            var report = CrashReport.ForThrowable(e, "Updated chunk");
            var details = report.AddCategory("Updated chunk details");
            details.SetDetail("Data version", version);
            details.SetDetail("Target version", targetVersion);
            throw new ReportedException(report);
        }
    }

    public CompoundTag UpgradeChunkTag(CompoundTag chunkTag, int defaultVersion)
        => UpgradeChunkTag(chunkTag, defaultVersion, null, SharedConstants.WorldDataVersion);

    public Dynamic<Tag> UpgradeChunkTag(Dynamic<Tag> chunkTag, int defaultVersion)
        => new(chunkTag.Ops, UpgradeChunkTag((CompoundTag)chunkTag.Value, defaultVersion));

    //Inject the DataFixer context tag, maps to vanilla injectDatafixingContext
    public static void InjectDatafixingContext(CompoundTag chunkTag, CompoundTag? contextTag)
    {
        if (contextTag != null) chunkTag.Put(DatafixerContextTag, contextTag);
    }

    private static void RemoveDatafixingContext(CompoundTag chunkTag)
        => chunkTag.Remove(DatafixerContextTag);

    public Task Synchronize(bool flush) => _worker.Synchronize(flush);

    public ChunkScanAccess ChunkScanner() => _worker;

    public RegionStorageInfo StorageInfo() => _worker.StorageInfo();

    public void Dispose() => _worker.Dispose();
}
