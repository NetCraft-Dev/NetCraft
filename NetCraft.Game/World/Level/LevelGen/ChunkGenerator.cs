using NetCraft.Game.World.Level.LevelGen.Features;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkGenerator 区块生成器抽象基类对应原版 net.minecraft.world.level.chunk.ChunkGenerator
//持有 BiomeSource 子类按需实现噪声/表面/高度/列采样等方法
//BuildSurface/FillFromNoise 等方法签名对齐原版占位参数用 object 待 WorldGenRegion/StructureManager 就绪升级
public abstract class ChunkGenerator
{
    public BiomeSource BiomeSource { get; }

    //WorldSeed 世界种子 由生成链装配时注入 对应原版 createState 拿到的那个种子
    //噪声随机状态必须按它建: 每区块派生的局部随机源不能当种子 谁先抢到初始化谁定整局地形
    public long WorldSeed { get; set; }

    protected ChunkGenerator(BiomeSource biomeSource)
    {
        BiomeSource = biomeSource;
    }

    //GetGenDepth 最大生成深度对应原版 getGenDepth
    //表示区块从最低到最高的总高度待子类按 settings 推导
    public abstract int GetGenDepth();

    //GetMinY 生成范围最低方块 Y 对应原版 getMinY
    //WorldGenerationContext 用它把锚点解算成绝对 Y
    public abstract int GetMinY();

    //GetBaseHeight 采样指定坐标的基础高度对应原版 getBaseHeight
    //type 参数为 HeightmapTypes 占位用 int待 Heightmap 子系统就绪升级
    public abstract int GetBaseHeight(int x, int z, int type, LevelHeightAccessor level, RandomSource random);

    //GetBaseColumn 采样指定坐标的基础列方块状态对应原版 getBaseColumn
    //返回 BlockState[] 简化占位用 object[] 待 BlockState 在 Game 层就绪升级
    public abstract object[] GetBaseColumn(int x, int z, LevelHeightAccessor level, RandomSource random);

    //FillFromNoise 从噪声填方块到区块对应原版 fillFromNoise
    //blender/structures 占位用 object 待子系统就绪升级
    public abstract void FillFromNoise(object blender, object structures, ChunkAccess chunk, RandomSource random);

    //BuildSurface 应用表面规则到区块对应原版 buildSurface
    //region/structures 占位用 object 待子系统就绪升级
    public abstract void BuildSurface(object region, object structures, ChunkAccess chunk, RandomSource random);

    //ApplyCarvers 在地表之后装饰之前雕刻洞穴对应原版 applyCarvers
    //seed 与 index 决定每个雕刻器的起点随机 randomState 由实现内部按 random 取
    public abstract void ApplyCarvers(long seed, ChunkAccess chunk, RandomSource random);

    //ApplyBiomeDecoration 应用生物群系装饰对应原版 applyBiomeDecoration
    //region 是含 3x3 区块的生成区域 structures 收集本流水线装配的结构结果
    public abstract void ApplyBiomeDecoration(WorldGenRegion region, ChunkAccess chunk,
        StructureFeatureManager structures);

    //FeaturesPerStep 各 step 下按全局索引排好序的特征表 首次访问时按可能群系构建并缓存
    //排序保证同一位置在不同区块视角下的放置顺序一致 否则同一个种子会长出不同地形
    private IReadOnlyList<FeatureSorter.StepFeatureData>? _featuresPerStep;

    public IReadOnlyList<FeatureSorter.StepFeatureData> FeaturesPerStep
        => _featuresPerStep ??= FeatureSorter.BuildFeaturesPerStep(
            BiomeSource.PossibleBiomes, biome => biome.Generation.Features);
}
