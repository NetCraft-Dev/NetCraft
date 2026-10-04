using NetCraft.Codec;
using NetCraft.Config;
using NetCraft.DataFixer;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Util;

namespace NetCraft.Storage;

//通用区域存储对应原版SimpleRegionStorage
//持有IOWorker封装异步IO对外提供read/write/synchronize/chunkScanner等高层API
//持有DataFixer与DataFixTypes执行upgradeChunkTag真实路径
//错误包装为ReportedException对齐原版错误报告路径
public sealed class SimpleRegionStorage : IDisposable
{
    //数据修复上下文tag名对应原版ChunkHeightAndBiomeFix.DATAFIXER_CONTEXT_TAG
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

    //升级chunkTag到targetVersion对应原版upgradeChunkTag
    //委托DataFixTypes.update走DataFixer.update真实路径异常包装ReportedException
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

    //注入DataFixer上下文tag对应原版injectDatafixingContext
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
