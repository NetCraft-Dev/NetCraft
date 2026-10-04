using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Util;

namespace NetCraft.Storage.Light;

//ChunkSkyLightSources 区块天光光源列高度图对应原版 net.minecraft.world.level.lighting.ChunkSkyLightSources
//记录每列最低的天光入射高度 天光引擎据此跳过整段无遮挡的区段
public class ChunkSkyLightSources
{
    //整列都没有遮挡边界时的哨兵值
    public const int NegativeInfinity = int.MinValue;

    private const int Size = 16;

    private readonly int _minY;
    private readonly BitStorage _heightmap;

    public ChunkSkyLightSources(LevelHeightAccessor level)
    {
        //minY 取世界最低再下一格 用来表示"该列向下再无遮挡"
        _minY = level.MinBuildHeight - 1;
        var maxY = level.MaxBuildHeight;
        var bits = Mth.CeilLog2(maxY - _minY + 1);
        _heightmap = new SimpleBitStorage(bits, Size * Size);
    }

    //fillFrom 从区块内容重建每列的光源高度
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

    //findHighestFilledSectionY 原版由区块自己记录 这里自顶向下搜一次
    private static int? FindHighestFilledSectionY(ChunkAccess chunk)
    {
        for (var sectionY = chunk.MaxSectionY; sectionY >= chunk.MinSectionY; sectionY--)
        {
            var section = chunk.GetSection(sectionY);
            if (section is not null && !section.HasOnlyAir()) return sectionY;
        }
        return null;
    }

    //findLowestSourceY 自最高非空区段顶部向下找第一个遮挡边界
    private int FindLowestSourceY(ChunkAccess chunk, int topSectionY, int x, int z)
    {
        var topY = SectionPos.SectionToBlockCoord(topSectionY + 1);
        var topPosY = topY;
        var bottomPosY = topY - 1;
        //顶部之上一格按空气处理 空气的状态 id 为 0 与 Blocks 的注册顺序一致
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

    //update 方块变化后刷新该列 返回是否真的产生变化
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

    //isEdgeOccluded 上方方块底面与下方方块顶面合并后是否遮挡天光
    //原版还要比较两面的遮挡形状 形状体系未实现前先用整方块减光近似
    private static bool IsEdgeOccluded(BlockState topState, BlockState bottomState)
        => bottomState.GetLightDampening() != 0;

    //getLowestSourceY 取该列最低天光入射高度 整列无遮挡返回NegativeInfinity
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
