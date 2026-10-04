namespace NetCraft.Storage.Chunk;

//LevelHeightAccessor对应原版net.minecraft.world.level.LevelHeightAccessor
//提供区段Y范围与索引换算简化为单页height accessor无跨页逻辑
public interface LevelHeightAccessor
{
    int MinSectionY { get; }
    int MaxSectionY { get; }
    int SectionsCount { get; }

    //区段Y转区段索引越界返回-1对应原版getSectionIndexFromSectionY
    int GetSectionIndexFromSectionY(int sectionY)
        => sectionY >= MinSectionY && sectionY <= MaxSectionY ? sectionY - MinSectionY : -1;

    int MinBuildHeight => MinSectionY * 16;
    int MaxBuildHeight => (MaxSectionY + 1) * 16;
}

//简单实现用于测试与stub场景构造minSectionY与sectionsCount推导maxSectionY
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
