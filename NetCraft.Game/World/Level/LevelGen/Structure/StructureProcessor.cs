using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureBlockInfo 模板里的一个方块 对应原版 StructureTemplate.StructureBlockInfo
//Nbt 非空表示这一格还带方块实体数据 放置时会补建方块实体
public sealed record StructureBlockInfo(BlockPos Pos, BlockState State, NetCraft.Nbt.CompoundTag? Nbt);

//StructureEntityInfo 模板里的一个实体 对应原版 StructureTemplate.StructureEntityInfo
//Pos 是实体精确坐标 BlockPos 是它所在的方块坐标 Nbt 缺失时整条丢弃
public sealed record StructureEntityInfo(Vec3i Pos, BlockPos BlockPos, NetCraft.Nbt.CompoundTag Nbt);

//StructureTemplatePalette 一个调色板下的方块列表 对应原版 StructureTemplate.Palette
//多调色板的模板每次放置按坐标派生随机挑一个
public sealed class StructureTemplatePalette
{
    public IReadOnlyList<StructureBlockInfo> Blocks { get; }

    public StructureTemplatePalette(IReadOnlyList<StructureBlockInfo> blocks) => Blocks = blocks;
}

//StructureProcessor 结构处理器 对应原版同名接口
//按顺序把放置中的方块逐个过一遍 返回 null 表示丢弃这一格
//继承注册表侧标记接口 处理器类型才能登记进 STRUCTURE_PROCESSOR
public interface StructureProcessor : NetCraft.Registry.StructureProcessor
{
    //ProcessBlock 处理单个方块 targetPosition 是最终写入坐标 templateRelativePos 是模板内局部坐标
    StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
        => processedBlockInfo;

    //FinalizeProcessing 全部方块处理完后统一收尾
    IReadOnlyList<StructureBlockInfo> FinalizeProcessing(WorldGenRegion? level, BlockPos position, BlockPos referencePos,
        IReadOnlyList<StructureBlockInfo> originalBlockInfoList,
        IReadOnlyList<StructureBlockInfo> processedBlockInfoList, StructurePlaceSettings settings)
        => processedBlockInfoList;

    //EvaluatesEntirePieceState 是否要看整片结构的状态 为真时不做当前区块裁剪
    bool EvaluatesEntirePieceState() => false;

    //ElementCodec 该处理器的 JSON codec 供 processor_list 装载 未实现解析的处理器返回 null
    MapCodec<StructureProcessor>? ElementCodec => null;
}
