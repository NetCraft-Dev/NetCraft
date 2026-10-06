using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Util;

namespace NetCraft.Storage.Light;

//ChunkSkyLightSources, chunk sky light source column heightmap, maps to vanilla net.minecraft.world.level.lighting.ChunkSkyLightSources
//Records the lowest sky light entry height per column; the sky light engine uses it to skip fully unoccluded sections
public class ChunkSkyLightSources
{
    //Sentinel when a column has no occluding edge at all
    public const int NegativeInfinity = int.MinValue;

    private const int Size = 16;

    private readonly int _minY;
    private readonly BitStorage _heightmap;

    public ChunkSkyLightSources(LevelHeightAccessor level)
    {
        //minY is one below the world minimum, meaning "no further occlusion below this column"
        _minY = level.MinBuildHeight - 1;
        var maxY = level.MaxBuildHeight;
        var bits = Mth.CeilLog2(maxY - _minY + 1);
        _heightmap = new SimpleBitStorage(bits, Size * Size);
    }

    //fillFrom rebuilds each column's source height from chunk content
    public void FillFrom(ChunkAccess chunk)
    {
        if (FindHighestFilledSectionY(chunk) is not int maxSectionY)
        {
            Fill(_minY);
            return;
        }

        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
            Set(Index(x, z), Math.Max(FindLowestSourceY(chunk, maxSectionY, x, z), _minY));
    }

    //findHighestFilledSectionY: vanilla records it on the chunk; here it searches top-down once
    private static int? FindHighestFilledSectionY(ChunkAccess chunk)
    {
        for (var sectionY = chunk.MaxSectionY; sectionY >= chunk.MinSectionY; sectionY--)
        {
            var section = chunk.GetSection(sectionY);
            if (section is not null && !section.HasOnlyAir()) return sectionY;
        }
        return null;
    }

    //findLowestSourceY searches down from the top of the highest non-empty section for the first occluding edge
    private int FindLowestSourceY(ChunkAccess chunk, int topSectionY, int x, int z)
    {
        var topY = SectionPos.SectionToBlockCoord(topSectionY + 1);
        var topPosY = topY;
        var bottomPosY = topY - 1;
        //The cell above the top is treated as air; air's state id is 0, matching the Blocks registration order
        var topState = default(BlockState);

        for (var sectionY = topSectionY; sectionY >= chunk.MinSectionY; sectionY--)
        {
            var section = chunk.GetSection(sectionY);
            if (section is null || section.HasOnlyAir())
            {
                topState = default(BlockState);
                topPosY = SectionPos.SectionToBlockCoord(sectionY);
                bottomPosY = topPosY - 1;
                continue;
            }

            for (var y = 15; y >= 0; y--)
            {
                var bottomState = section.GetBlockState(x, y, z);
                if (IsEdgeOccluded(topState, bottomState)) return topPosY;
                topState = bottomState;
                topPosY = bottomPosY;
                bottomPosY--;
            }
        }
        return _minY;
    }

    //update refreshes that column after a block change, returns whether anything actually changed
    public bool Update(BlockGetter level, int x, int y, int z)
    {
        var upperEdgeY = y + 1;
        var index = Index(x, z);
        var currentLowestSourceY = Get(index);
        if (upperEdgeY < currentLowestSourceY) return false;

        var topY = y + 1;
        var topState = level.GetBlockState(x, topY, z);
        var middleY = y;
        var middleState = level.GetBlockState(x, middleY, z);
        if (UpdateEdge(level, index, currentLowestSourceY, x, z, topY, topState, middleY, middleState))
            return true;

        var bottomY = y - 1;
        var bottomState = level.GetBlockState(x, bottomY, z);
        return UpdateEdge(level, index, currentLowestSourceY, x, z, middleY, middleState, bottomY, bottomState);
    }

    private bool UpdateEdge(BlockGetter level, int index, int oldTopEdgeY, int x, int z,
        int topY, BlockState topState, int bottomY, BlockState bottomState)
    {
        if (IsEdgeOccluded(topState, bottomState))
        {
            if (topY > oldTopEdgeY)
            {
                Set(index, topY);
                return true;
            }
            return false;
        }

        if (topY != oldTopEdgeY) return false;
        Set(index, FindLowestSourceBelow(level, x, bottomY, z, bottomState));
        return true;
    }

    private int FindLowestSourceBelow(BlockGetter level, int x, int y, int z, BlockState startState)
    {
        var topY = y;
        var bottomY = y - 1;
        var topState = startState;
        while (bottomY >= _minY)
        {
            var bottomState = level.GetBlockState(x, bottomY, z);
            if (IsEdgeOccluded(topState, bottomState)) return topY;
            topState = bottomState;
            topY = bottomY;
            bottomY--;
        }
        return _minY;
    }

    //isEdgeOccluded, whether the merged bottom of the upper block and top of the lower block occlude sky light
    //Vanilla also compares the occlusion shapes of both faces; before the shape system exists this approximates with full-block light dampening
    private static bool IsEdgeOccluded(BlockState topState, BlockState bottomState)
        => bottomState.GetLightDampening() != 0;

    //getLowestSourceY returns the column's lowest sky light entry height; a fully unoccluded column returns NegativeInfinity
    public int GetLowestSourceY(int x, int z) => ExtendSourcesBelowWorld(Get(Index(x, z)));

    public int GetHighestLowestSourceY()
    {
        var maxValue = int.MinValue;
        for (var i = 0; i < _heightmap.Size; i++)
        {
            var value = _heightmap.Get(i);
            if (value > maxValue) maxValue = value;
        }
        return ExtendSourcesBelowWorld(maxValue + _minY);
    }

    private void Fill(int lowestSourceY)
    {
        var value = lowestSourceY - _minY;
        for (var i = 0; i < _heightmap.Size; i++) _heightmap.Set(i, value);
    }

    private void Set(int index, int value) => _heightmap.Set(index, value - _minY);

    private int Get(int index) => _heightmap.Get(index) + _minY;

    private int ExtendSourcesBelowWorld(int value) => value == _minY ? NegativeInfinity : value;

    private static int Index(int x, int z) => x + z * Size;
}
