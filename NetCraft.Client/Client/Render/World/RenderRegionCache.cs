using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render.Model;
using NetCraft.Gpu;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using Direction = NetCraft.Gpu.Direction;

namespace NetCraft.Game.Client.Render.World;

//RenderRegionCache 编译时跨 section 邻居方块查询解决 W7 越界视为 air 的边界面多渲染
//Snapshot 一次性取 center 周围 3x3x3 共 27 个 LevelChunkSection 引用浅快照不深拷贝
//GetBlockState 世界坐标查对应 section 的局部方块邻居未加载返回 air
//ShouldCullFace 供 ChunkMeshBuilder 跨 section 面剔除查面外侧邻居是否 FullBlock
//Direction 用 NetCraft.Gpu.Direction(enum)与 ChunkMeshBuilder/BakedQuad 一致 UnitVector 取方向向量
public sealed class RenderRegionCache
{
    private readonly SectionPos _center;
    //_sections[dx+1,dy+1,dz+1] dx/dy/dz 范围 -1..1 null 表示邻居 section 未加载
    private readonly LevelChunkSection?[,,] _sections = new LevelChunkSection[3, 3, 3];

    public SectionPos Center => _center;

    private RenderRegionCache(SectionPos center) => _center = center;

    //Snapshot 取 center 周围 3x3x3 section 引用持 ClientLevel 读锁避免编译中途字典被改
    public static RenderRegionCache Snapshot(ClientLevel level, SectionPos center)
    {
        var cache = new RenderRegionCache(center);
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
            cache._sections[dx + 1, dy + 1, dz + 1] = level.GetSection(center.X + dx, center.Y + dy, center.Z + dz);
        return cache;
    }

    //GetBlockState 世界坐标查邻居方块越界 3x3x3 或 section 未加载返回 air
    public BlockState GetBlockState(int worldX, int worldY, int worldZ)
    {
        var sectionX = worldX >> 4;
        var sectionY = worldY >> 4;
        var sectionZ = worldZ >> 4;
        var dx = sectionX - _center.X;
        var dy = sectionY - _center.Y;
        var dz = sectionZ - _center.Z;
        if ((uint)(dx + 1) > 2 || (uint)(dy + 1) > 2 || (uint)(dz + 1) > 2)
            return default;
        var section = _sections[dx + 1, dy + 1, dz + 1];
        if (section is null) return default;
        return section.GetBlockState(worldX & 15, worldY & 15, worldZ & 15);
    }

    //ShouldCullFace 查面外侧邻居是否 FullBlock 供 ChunkMeshBuilder 跨 section 面剔除
    //邻居 section 未加载时返回 false 保守不剔除等邻居加载后重编译剔除与 W7 越界视为 air 一致
    public bool ShouldCullFace(int worldX, int worldY, int worldZ, Direction dir)
    {
        var offset = dir.UnitVector();
        var nx = worldX + (int)offset.X;
        var ny = worldY + (int)offset.Y;
        var nz = worldZ + (int)offset.Z;
        if (!HasSection(nx, ny, nz)) return false;
        var neighbor = GetBlockState(nx, ny, nz);
        return BlockRenderShapeProvider.GetShape(neighbor) == BlockRenderShape.FullBlock;
    }

    //HasSection 邻居坐标对应 section 是否在 3x3x3 快照内且已加载
    private bool HasSection(int worldX, int worldY, int worldZ)
    {
        var sectionX = worldX >> 4;
        var sectionY = worldY >> 4;
        var sectionZ = worldZ >> 4;
        var dx = sectionX - _center.X;
        var dy = sectionY - _center.Y;
        var dz = sectionZ - _center.Z;
        if ((uint)(dx + 1) > 2 || (uint)(dy + 1) > 2 || (uint)(dz + 1) > 2)
            return false;
        return _sections[dx + 1, dy + 1, dz + 1] is not null;
    }
}
