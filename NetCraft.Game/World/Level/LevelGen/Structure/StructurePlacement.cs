using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePlacement 结构放置抽象 对应原版 net.minecraft.world.level.levelgen.structure.placement.StructurePlacement
//决定结构落在哪些区块 以及频率削减与其他结构集合之间的排斥
//子类只实现 IsPlacementChunk 给出网格判定 其余判定由基类组合
public abstract class StructurePlacement
{
    //HighlyArbitraryRandomSalt 遗留盐值 对应原版 HIGHLY_ARBITRARY_RANDOM_SALT
    //只有 LEGACY_TYPE_2 用它 换别的值会让老存档的结构位置整体偏移
    public const int HighlyArbitraryRandomSalt = 10387320;

    //LocateOffset 定位点偏移 对应原版 locate_offset
    //只影响 /locate 报出的坐标 不参与区块判定
    public Vec3i LocateOffset { get; }

    //ReductionMethod 频率削减算法 对应原版 frequency_reduction_method
    public FrequencyReductionMethod ReductionMethod { get; }

    //Frequency 命中概率 默认 1.0 表示不做削减
    public float Frequency { get; }

    //Salt 盐值 参与种子派生
    public int Salt { get; }

    //Exclusion 排斥区 附近存在另一个集合的结构时本结构不生成
    //属性不能与嵌套类型 ExclusionZone 同名 故这里用短名
    public ExclusionZone? Exclusion { get; }

    protected StructurePlacement(Vec3i locateOffset, FrequencyReductionMethod reductionMethod, float frequency,
        int salt, ExclusionZone? exclusionZone)
    {
        LocateOffset = locateOffset;
        ReductionMethod = reductionMethod;
        Frequency = frequency;
        Salt = salt;
        Exclusion = exclusionZone;
    }

    //IsPlacementChunk 基础网格判定 由子类实现
    protected abstract bool IsPlacementChunk(ChunkGeneratorStructureState state, int sourceX, int sourceZ);

    //IsStructureChunk 最终判定 对应原版 isStructureChunk
    //三段与关系 基础网格 频率削减 排斥区 顺序不能颠倒因频率削减会消耗随机数
    public bool IsStructureChunk(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
        => IsPlacementChunk(state, sourceX, sourceZ)
            && ApplyAdditionalChunkRestrictions(sourceX, sourceZ, state.LevelSeed)
            && ApplyInteractionsWithOtherStructures(state, sourceX, sourceZ);

    //ApplyAdditionalChunkRestrictions 频率削减 对应原版 applyAdditionalChunkRestrictions
    //Frequency 为 1 时整段跳过 连一次随机数都不消耗 这点影响后续判定结果
    public bool ApplyAdditionalChunkRestrictions(int sourceX, int sourceZ, long levelSeed)
        => Frequency >= 1.0f || ReductionMethod.ShouldGenerate(levelSeed, Salt, sourceX, sourceZ, Frequency);

    //ApplyInteractionsWithOtherStructures 排斥区判定 对应原版 applyInteractionsWithOtherStructures
    public bool ApplyInteractionsWithOtherStructures(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
        => Exclusion is null || !Exclusion.IsPlacementForbidden(state, sourceX, sourceZ);

    //GetLocatePos 定位坐标 对应原版 getLocatePos
    public BlockPos GetLocatePos(ChunkPos chunkPos)
        => new(chunkPos.X << 4, LocateOffset.Y, chunkPos.Z << 4);

    //ExclusionZone 排斥区 对应原版 StructurePlacement.ExclusionZone
    //OtherSet 是另一个结构集合 ChunkCount 是检查半径 1..16
    public sealed record ExclusionZone(Holder<NetCraft.Registry.StructureSet> OtherSet, int ChunkCount)
    {
        //IsPlacementForbidden 该范围内命中了排斥集合的结构则禁止生成
        //私有成员对包含类可见 外层直接调用即可
        internal bool IsPlacementForbidden(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
            => state.HasStructureChunkInRange(OtherSet, sourceX, sourceZ, ChunkCount);
    }
}

//FrequencyReductionMethod 频率削减算法 对应原版 StructurePlacement.FrequencyReductionMethod
//四种算法的随机源与比较精度都不同 数值必须逐字对齐 否则结构分布会与原版不同
public enum FrequencyReductionMethod
{
    Default,
    LegacyType1,
    LegacyType2,
    LegacyType3,
}

//FrequencyReductionMethods 削减算法的实现与名字映射
public static class FrequencyReductionMethods
{
    //ShouldGenerate 判定该区块是否通过频率削减 对应原版各 reducer
    //四种都新建 LegacyRandomSource(0) 再重播种子 保证与调用顺序无关
    public static bool ShouldGenerate(this FrequencyReductionMethod method, long seed, int salt,
        int sourceX, int sourceZ, float probability)
    {
        var random = new LegacyRandomSource(0L);
        switch (method)
        {
            case FrequencyReductionMethod.LegacyType1:
                //前哨站用 按区块坐标异或后取倒数区间命中 概率语义是 1/probability 分之一
                var chunkX = sourceX >> 4;
                var chunkZ = sourceZ >> 4;
                random.SetSeed((chunkX ^ (chunkZ << 4)) ^ seed);
                random.NextInt();
                return random.NextInt((int)(1.0f / probability)) == 0;
            case FrequencyReductionMethod.LegacyType2:
                //带遗留盐值 比较用 float
                WorldgenRandom.SetLargeFeatureWithSalt(random, seed, sourceX, sourceZ,
                    StructurePlacement.HighlyArbitraryRandomSalt);
                return random.NextFloat() < probability;
            case FrequencyReductionMethod.LegacyType3:
                //按区块坐标派生 比较用 double 精度比 float 高
                WorldgenRandom.SetLargeFeatureSeed(random, seed, sourceX, sourceZ);
                return random.NextDouble() < probability;
            default:
                //带普通盐值 比较用 float
                WorldgenRandom.SetLargeFeatureWithSalt(random, seed, salt, sourceX, sourceZ);
                return random.NextFloat() < probability;
        }
    }

    //Name 取 JSON 名 对应原版 getSerializedName
    public static string Name(this FrequencyReductionMethod method) => method switch
    {
        FrequencyReductionMethod.LegacyType1 => "legacy_type_1",
        FrequencyReductionMethod.LegacyType2 => "legacy_type_2",
        FrequencyReductionMethod.LegacyType3 => "legacy_type_3",
        _ => "default",
    };

    //TryParse 按 JSON 名解析 名字非法返回 null
    public static FrequencyReductionMethod? TryParse(string name) => name switch
    {
        "default" => FrequencyReductionMethod.Default,
        "legacy_type_1" => FrequencyReductionMethod.LegacyType1,
        "legacy_type_2" => FrequencyReductionMethod.LegacyType2,
        "legacy_type_3" => FrequencyReductionMethod.LegacyType3,
        _ => null,
    };
}
