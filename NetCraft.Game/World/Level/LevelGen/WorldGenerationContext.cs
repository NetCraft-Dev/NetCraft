using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//WorldGenerationContext world generation context, maps to vanilla net.minecraft.world.level.levelgen.WorldGenerationContext
//Clamps the generator and height accessor into the Y range that can actually be generated; height providers and carving use it to resolve anchors into absolute Y
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
