namespace NetCraft.Gpu;

//BakedModel 烘焙后模型纯数据容器对标原版 BakedModel
//按 (RenderLayer, Direction?) 二维分组存储 BakedQuad
//Direction? null 表示无 cullface 总是渲染 Direction 值表示该方向 cullface 参与面剔除
//Gpu 层只提供数据结构不含业务逻辑（解析/映射由 Game 层做）
//BlockModelBaker 烘焙 UnbakedModel+图集 UV→BakedModel
//ChunkMeshBuilder 按 layer 遍历 cullface quad 查邻居剔除 no-cull quad 总是渲染
public sealed class BakedModel
{
    //按 (layer, cullface) 二维分组 cullface=null 表示 no-cull quad
    private readonly Dictionary<(RenderLayer, Direction?), List<BakedQuad>> _quads = new();
    //Layers 缓存避免每次查询分配
    private HashSet<RenderLayer>? _layersCache;

    //GetQuads 获取指定 layer 的所有 quad（含 cullface 和 no-cull）向后兼容
    public IReadOnlyList<BakedQuad> GetQuads(RenderLayer layer)
    {
        List<BakedQuad>? result = null;
        foreach (var ((l, _), list) in _quads)
        {
            if (l != layer) continue;
            (result ??= new List<BakedQuad>()).AddRange(list);
        }
        return (IReadOnlyList<BakedQuad>?)result ?? Array.Empty<BakedQuad>();
    }

    //GetCullfaceQuads 获取指定方向所有 layer 的 cullface quad 向后兼容
    //chunk mesh 生成应优先用 GetCullfaceQuads(layer, dir) 避免 cross-layer 混淆
    public IReadOnlyList<BakedQuad> GetCullfaceQuads(Direction dir)
    {
        List<BakedQuad>? result = null;
        foreach (var ((_, d), list) in _quads)
        {
            if (d != dir) continue;
            (result ??= new List<BakedQuad>()).AddRange(list);
        }
        return (IReadOnlyList<BakedQuad>?)result ?? Array.Empty<BakedQuad>();
    }

    //GetCullfaceQuads 获取指定 layer + 方向的 cullface quad 供 chunk mesh 面剔除查询
    public IReadOnlyList<BakedQuad> GetCullfaceQuads(RenderLayer layer, Direction dir)
        => _quads.TryGetValue((layer, dir), out var list) ? list : Array.Empty<BakedQuad>();

    //GetNoCullQuads 获取指定 layer 无 cullface 的 quad 总是渲染不参与面剔除
    public IReadOnlyList<BakedQuad> GetNoCullQuads(RenderLayer layer)
        => _quads.TryGetValue((layer, null), out var list) ? list : Array.Empty<BakedQuad>();

    //AddQuad 添加 quad 到指定 layer 和 cullface 分组
    //cullface=null 放 NoCull 不参与 face culling 总是渲染
    public void AddQuad(RenderLayer layer, BakedQuad quad, Direction? cullface)
    {
        var key = (layer, cullface);
        if (!_quads.TryGetValue(key, out var list))
        {
            list = new List<BakedQuad>();
            _quads[key] = list;
            _layersCache = null;
        }
        list.Add(quad);
    }

    //Layers 返回所有存在的 RenderLayer
    public IEnumerable<RenderLayer> Layers
    {
        get
        {
            if (_layersCache is null)
            {
                _layersCache = new HashSet<RenderLayer>();
                foreach (var (l, _) in _quads.Keys)
                    _layersCache.Add(l);
            }
            return _layersCache;
        }
    }
}

//RenderLayer 渲染层级对标原版 RenderType
//Solid 不透明方块最早渲染写深度
//Cutout 透明像素方块（玻璃）alpha=0 或 1 用 alpha test
//Translucent 半透明方块（水）按距离排序后渲染
public enum RenderLayer
{
    Solid,
    Cutout,
    Translucent
}
