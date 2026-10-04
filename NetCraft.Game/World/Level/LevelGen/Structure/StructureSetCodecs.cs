using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureSetCodecs 结构集合 codec 入口 对应原版 StructureSet.CODEC
public static class StructureSetCodecs
{
    //ElementCodec 结构集合注册表元素 codec
    public static readonly Codec<NetCraft.Registry.StructureSet> ElementCodec = new StructureSetCodec();
}

//StructureSetCodec 解析一个放置配置与按权重抽取的结构列表 对应原版 StructureSet.CODEC
internal sealed class StructureSetCodec : ScalarCodec<NetCraft.Registry.StructureSet>
{
    public override DataResult<NetCraft.Registry.StructureSet> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "结构集合必须是对象");
        var map = mapResult.GetOrThrow();

        var placementTag = map.Get("placement");
        if (!placementTag.IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "结构集合缺少 placement");
        var placementError = string.Empty;
        var placement = StructurePlacementCodecs.ElementCodec.Parse(ops, placementTag.Get())
            .ResultOrPartial(message => placementError = message);
        if (!placement.IsPresent)
            return DataResult<NetCraft.Registry.StructureSet>.Error(() => $"结构集合的 placement 解析失败 {placementError}");

        var structuresTag = map.Get("structures");
        if (!structuresTag.IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "结构集合缺少 structures");
        var stream = ops.GetStream(structuresTag.Get());
        if (!stream.Result().IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "structures 必须是数组");

        var entries = new List<StructureSelectionEntry>();
        foreach (var element in stream.GetOrThrow())
        {
            var entryResult = ops.GetMap(element);
            if (!entryResult.Result().IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "structures 的元素必须是对象");
            var entry = entryResult.GetOrThrow();
            var structureTag = entry.Get("structure");
            if (!structureTag.IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "structures 元素缺少 structure");
            var structureError = string.Empty;
            var structure = HolderSetCodecs.StructureRef.Parse(ops, structureTag.Get())
                .ResultOrPartial(message => structureError = message);
            if (!structure.IsPresent)
                return DataResult<NetCraft.Registry.StructureSet>.Error(() => $"structures 元素的 structure 解析失败 {structureError}");
            var weight = StructurePlacementCodecs.ReadIntField(ops, entry, "weight");
            if (!weight.Result().IsPresent) return DataResult<NetCraft.Registry.StructureSet>.Error(() => "structures 元素缺少 weight");
            entries.Add(new StructureSelectionEntry(structure.Get(), weight.GetOrThrow()));
        }

        return DataResult<NetCraft.Registry.StructureSet>.Success(
            new StructureSet(placement.Get(), entries));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.StructureSet value)
        => DataResult<U>.Error(() => "结构集合编码暂未实现");
}

//StructureBootstrap 结构子系统类型注册 对应原版 BuiltInRegistries 的结构类型登记
//各类型的静态 Instance 幂等注册 这里统一触碰一次保证在注册表 freeze 之前完成
public static class StructureBootstrap
{
    public static void RegisterAll()
    {
        _ = RandomSpreadStructurePlacementType.Instance;
    }
}
