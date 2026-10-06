namespace NetCraft.Storage.Chunk;

//LevelHeightAccessor, maps to vanilla net.minecraft.world.level.LevelHeightAccessor
//Provides the section Y range and index conversion; simplified to a single-page height accessor with no page crossing
public interface LevelHeightAccessor
{
    int MinSectionY { get; }
    int MaxSectionY { get; }
    int SectionsCount { get; }

    //Convert section Y to a section index; out of range returns -1, maps to vanilla getSectionIndexFromSectionY
    int GetSectionIndexFromSectionY(int sectionY)
        => sectionY >= MinSectionY && sectionY <= MaxSectionY ? sectionY - MinSectionY : -1;

    int MinBuildHeight => MinSectionY * 16;
    int MaxBuildHeight => (MaxSectionY + 1) * 16;
}

//Simple implementation for tests and stubs; constructs from minSectionY and sectionsCount, derives maxSectionY
public sealed class SimpleLevelHeightAccessor : LevelHeightAccessor
{
    public int MinSectionY { get; }
    public int MaxSectionY { get; }
    public int SectionsCount { get; }

    public SimpleLevelHeightAccessor(int minSectionY, int sectionsCount)
    {
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
        MaxSectionY = minSectionY + sectionsCount - 1;
    }
}
