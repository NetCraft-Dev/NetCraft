using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render.Model;
using NetCraft.Gpu;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using Direction = NetCraft.Gpu.Direction;

namespace NetCraft.Game.Client.Render.World;

//RenderRegionCache compile-time cross-section neighbor block query, fixing the over-rendering of boundary faces that W7 treats as air out of bounds
//Snapshot takes a shallow snapshot of the 27 LevelChunkSection references around center in a 3x3x3, no deep copy
//GetBlockState looks up the local block neighbor in the matching section by world coordinates; returns air when not loaded
//ShouldCullFace for ChunkMeshBuilder's cross-section face culling: checks whether the face's outer neighbor is FullBlock
//Direction uses NetCraft.Gpu.Direction (enum) for consistency with ChunkMeshBuilder/BakedQuad; UnitVector gives the direction vector
public sealed class RenderRegionCache
{
    private readonly SectionPos _center;
    //_sections[dx+1,dy+1,dz+1] with dx/dy/dz in -1..1; null means the neighbor section is not loaded
    private readonly LevelChunkSection?[,,] _sections = new LevelChunkSection[3, 3, 3];

    public SectionPos Center => _center;

    private RenderRegionCache(SectionPos center) => _center = center;

    //Snapshot takes 3x3x3 section references around center while holding ClientLevel's read lock to avoid the dictionary being modified mid-compile
    public static RenderRegionCache Snapshot(ClientLevel level, SectionPos center)
    {
        var cache = new RenderRegionCache(center);
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
            cache._sections[dx + 1, dy + 1, dz + 1] = level.GetSection(center.X + dx, center.Y + dy, center.Z + dz);
        return cache;
    }

    //GetBlockState looks up a neighbor block by world coordinates; returns air when out of the 3x3x3 or the section is not loaded
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

    //ShouldCullFace checks whether the face's outer neighbor is FullBlock, for ChunkMeshBuilder's cross-section face culling
    //Returns false when the neighbor section is not loaded, conservatively not culling; once the neighbor loads it recompiles and culls, consistent with W7 treating out-of-bounds as air
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

    //HasSection whether the section for the neighbor coordinates is in the 3x3x3 snapshot and loaded
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
