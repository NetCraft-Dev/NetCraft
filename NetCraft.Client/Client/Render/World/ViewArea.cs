using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Render.Culling;
using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Render.World;

//ViewArea 视锥内 section 列表管理对标原版 ViewArea
//Update 遍历 loaded chunks 测 frustum diff 出新可见 section 调 onNewSection 触发编译
//onRemoved 首版不处理 mesh 保留在 dispatcher._sections 下次再可见直接用
//_visibleSections 只在 Render 线程访问无并发不需同步原语
public sealed class ViewArea
{
    private HashSet<long> _visibleSections = new();

    public int VisibleCount => _visibleSections.Count;

    //Update 遍历 level.GetLoadedChunks 所有 section 测 frustum 收集新可见集合
    //newVisible 中不在 _visibleSections 的调 onNewSection 触发 dispatcher.MarkDirty
    //首版不调 onRemoved 移除的 section mesh 保留复用 LRU 卸载留优化
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
