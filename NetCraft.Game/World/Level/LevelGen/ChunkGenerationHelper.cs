using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkGenerationHelper 区块生成辅助类
//把 ChunkGenerator + ChunkStatusProcessor 包装成 ServerChunkCache 需要的 generator 回调
//存档未命中时按 ChunkStatus 状态机流水线从 EMPTY 推进到 FULL 生成新 chunk
public static class ChunkGenerationHelper
{
    //CreateGenerator 创建 generator 闭包 同时把共享的结构管理器交回调用方供落盘与读档使用
    //minSectionY/sectionsCount 用于构造 SimpleLevelHeightAccessor 决定生成 chunk 的区段范围
    //worldSeed 用于结构放置判定 chunkProvider 提供邻块 未接时装饰退化为只看中心区块
    public static (Func<ChunkPos, ChunkAccess?> Generator, StructureFeatureManager Structures) CreateGenerator(
        ChunkGenerator generator,
        int minSectionY,
        int sectionsCount,
        RandomSource random,
        PalettedContainerFactory factory,
        long worldSeed = 0L,
        Func<int, int, ChunkAccess?>? chunkProvider = null,
        bool generateStructures = true)
    {
        var level = new SimpleLevelHeightAccessor(minSectionY, sectionsCount);
        //世界种子注入生成器 噪声随机状态与结构放置都按它派生 与调用方随机源解耦
        generator.WorldSeed = worldSeed;
        //基准种子只取一次 之后每区块由坐标派生独立随机源
        var baseSeed = random.NextLong();
        var structureRegistry = BuildStructureRegistry(worldSeed);
        //结构结果整个维度共享一份 每个区块各建一份的话装饰阶段看不到邻块已装配的结构
        var structures = new StructureFeatureManager(generator, structureRegistry)
        {
            //服务端配置 generate-structures 关掉后连结构模板都不会加载
            ShouldGenerateStructures = generateStructures,
        };
        Func<ChunkPos, ChunkAccess?> generatorFunc = pos =>
        {
            //每个区块用独立 processor 与随机源 生成在线程池并发执行
            //共享实例会破坏 RandomSource 内部状态导致生成卡死
            //按坐标派生种子保证同一区块可重现
            var processor = new ChunkStatusProcessor(generator, RandomSource.Create(baseSeed + pos.Pack()),
                structureRegistry, worldSeed, chunkProvider, structures);
            return GenerateChunk(processor, level, factory, pos);
        };
        return (generatorFunc, structures);
    }

    //BuildStructureRegistry 把已装载的结构集合灌进放置注册表
    //对应原版 ChunkGenerator 构造时持有的全部结构集合 缺一个该集合的结构就永远不生成
    private static StructurePlacementRegistry BuildStructureRegistry(long worldSeed)
    {
        var registry = new StructurePlacementRegistry(worldSeed);
        foreach (var holder in BuiltInRegistries.STRUCTURE_SET.ListElements())
        {
            //Registry 层与 Game 层都有 StructureSet 名字 Game 层才是带放置参数的实际类型
            if (holder.Value is NetCraft.Game.World.Level.LevelGen.Structure.StructureSet set)
                registry.AddSet(set);
        }
        return registry;
    }

    //GenerateChunk 单 chunk 生成流程对应原版 chunk generator 流水线
    //1. 构造 ProtoChunk 状态 EMPTY
    //2. 走 ChunkStatusProcessor.ProcessToStatus 推进到 FULL
    //3. 返回 proto 供 ServerChunkCache 缓存
    private static ChunkAccess GenerateChunk(
        ChunkStatusProcessor processor,
        LevelHeightAccessor level,
        PalettedContainerFactory factory,
        ChunkPos pos)
    {
        var proto = new ProtoChunk(
            pos,
            level.MinSectionY,
            level.SectionsCount,
            factory.CreateForBlockStates,
            factory.CreateForBiomes);
        processor.ProcessToStatus(proto, ChunkStatus.FULL);
        return proto;
    }
}
