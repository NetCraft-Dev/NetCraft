using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//WorldGenerationContext 世界生成上下文对应原版 net.minecraft.world.level.levelgen.WorldGenerationContext
//把生成器与高度访问器夹取成实际能生成的 Y 区间 高度提供者与雕刻都靠它把锚点解算成绝对 Y
public class WorldGenerationContext
{
    private readonly int _minY;
    private readonly int _height;

    public WorldGenerationContext(ChunkGenerator generator, LevelHeightAccessor heightAccessor)
    {
        _minY = Math.Max(heightAccessor.MinBuildHeight, generator.GetMinY());
        _height = Math.Min(heightAccessor.SectionsCount * 16, generator.GetGenDepth());
    }

    public int GetMinGenY() => _minY;

    public int GetGenDepth() => _height;
}
