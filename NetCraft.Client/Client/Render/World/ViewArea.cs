using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render.Culling;
using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Render.World;

//ViewArea manages the list of sections inside the frustum, maps to vanilla ViewArea
//Update walks loaded chunks, tests the frustum, diffs out newly visible sections, and calls onNewSection to trigger compilation
//onRemoved is not handled in the first version; meshes stay in dispatcher._sections and are reused when visible again
//_visibleSections is only accessed on the Render thread; no concurrency, so no synchronization primitives needed
public sealed class ViewArea
{
    private HashSet<long> _visibleSections = new();

    public int VisibleCount => _visibleSections.Count;

    //Update walks all sections of level.GetLoadedChunks, tests the frustum, and collects the newly visible set
    //For entries in newVisible not in _visibleSections, call onNewSection to trigger dispatcher.MarkDirty
    //The first version does not call onRemoved; removed section meshes are kept for reuse, with LRU unloading left as an optimization
    public void Update(Frustum frustum, ClientLevel level, Action<SectionPos>? onNewSection = null)
    {
        var newVisible = new HashSet<long>();
        foreach (var chunk in level.GetLoadedChunks())
        {
            var sectionsCount = chunk.SectionsCount;
            for (var sy = 0; sy < sectionsCount; sy++)
            {
                var section = chunk.GetSection(sy);
                if (section is null || section.HasOnlyAir()) continue;
                var originX = chunk.Pos.X * 16;
                var originY = sy * 16;
                var originZ = chunk.Pos.Z * 16;
                var aabb = new AABB(originX, originY, originZ, originX + 16, originY + 16, originZ + 16);
                if (!frustum.IsVisible(aabb)) continue;
                var key = SectionPos.AsLong(chunk.Pos.X, sy, chunk.Pos.Z);
                newVisible.Add(key);
                if (!_visibleSections.Contains(key))
                    onNewSection?.Invoke(new SectionPos(chunk.Pos.X, sy, chunk.Pos.Z));
            }
        }
        _visibleSections = newVisible;
    }

    public bool IsVisible(SectionPos pos) => _visibleSections.Contains(pos.AsLong());
}
