using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RandomSpreadType 网格内随机偏移的分布 对应原版 RandomSpreadType
//线性是均匀分布 三角分布偏向网格中部 村庄用线性要塞用环形走别的路径
public enum RandomSpreadType
{
    Linear,
    Triangular,
}

//RandomSpreadTypes 分布实现与名字映射
public static class RandomSpreadTypes
{
    //Evaluate 取网格内偏移 对应原版 evaluate
    //三角分布取两次随机数的平均 消耗两次随机数这点会影响后续 spreadZ 的取值
    public static int Evaluate(this RandomSpreadType type, RandomSource random, int limit)
        => type == RandomSpreadType.Triangular
            ? (random.NextInt(limit) + random.NextInt(limit)) / 2
            : random.NextInt(limit);

    //Name 取 JSON 名
    public static string Name(this RandomSpreadType type)
        => type == RandomSpreadType.Triangular ? "triangular" : "linear";

    //TryParse 按 JSON 名解析 非法返回 null
    public static RandomSpreadType? TryParse(string name) => name switch
    {
        "linear" => RandomSpreadType.Linear,
        "triangular" => RandomSpreadType.Triangular,
        _ => null,
    };
}

//RandomSpreadStructurePlacement 随机散布放置 对应原版 RandomSpreadStructurePlacement
//按 spacing 划网格 每个网格用带盐种子选一个区块 命中即该网格的放置点
public sealed class RandomSpreadStructurePlacement : StructurePlacement
{
    public int Spacing { get; }
    public int Separation { get; }

    //SpreadType 网格内偏移分布
    public RandomSpreadType SpreadType { get; }

    public RandomSpreadStructurePlacement(Vec3i locateOffset, FrequencyReductionMethod reductionMethod,
        float frequency, int salt, ExclusionZone? exclusionZone, int spacing, int separation,
        RandomSpreadType spreadType = RandomSpreadType.Linear)
        : base(locateOffset, reductionMethod, frequency, salt, exclusionZone)
    {
        Spacing = spacing;
        Separation = separation;
        SpreadType = spreadType;
    }

    //便捷构造 只需网格参数与盐值时用 对应原版精简构造器
    public RandomSpreadStructurePlacement(int spacing, int separation, int salt,
        RandomSpreadType spreadType = RandomSpreadType.Linear)
        : this(Vec3i.Zero, FrequencyReductionMethod.Default, 1.0f, salt, null, spacing, separation, spreadType) { }

    //GetPotentialStructureChunk 算某个网格的放置区块 对应原版 getPotentialStructureChunk
    //网格坐标取 floorDiv 偏移用带盐种子派生 先 X 后 Z 连续消耗同一个随机源
    public ChunkPos GetPotentialStructureChunk(long seed, int sourceX, int sourceZ)
    {
        var gridX = FloorDiv(sourceX, Spacing);
        var gridZ = FloorDiv(sourceZ, Spacing);
        var random = new LegacyRandomSource(0L);
        WorldgenRandom.SetLargeFeatureWithSalt(random, seed, gridX, gridZ, Salt);
        var limit = Spacing - Separation;
        var spreadX = SpreadType.Evaluate(random, limit);
        var spreadZ = SpreadType.Evaluate(random, limit);
        return new ChunkPos(gridX * Spacing + spreadX, gridZ * Spacing + spreadZ);
    }

    //IsPlacementChunk 该区块恰好是本网格的放置点 对应原版 isPlacementChunk
    protected override bool IsPlacementChunk(ChunkGeneratorStructureState state, int sourceX, int sourceZ)
    {
        var potential = GetPotentialStructureChunk(state.LevelSeed, sourceX, sourceZ);
        return potential.X == sourceX && potential.Z == sourceZ;
    }

    //FloorDiv Java 语义向下取整除法 负数网格坐标对不上会让结构位置整体错位
    private static int FloorDiv(int a, int b)
    {
        var r = a / b;
        return (a ^ b) < 0 && r * b != a ? r - 1 : r;
    }
}
