namespace NetCraft.Gpu;

//BakedModel baked model pure-data container, maps to vanilla BakedModel
//Stores BakedQuads grouped two-dimensionally by (RenderLayer, Direction?)
//Direction? null means no cullface and always renders; a Direction value means that direction is culled
//The GPU layer only provides the data structures, no domain logic (parsing/mapping is done by the Game layer)
//BlockModelBaker bakes UnbakedModel+atlas UVs→BakedModel
//ChunkMeshBuilder walks cullface quads by layer and culls against neighbors; no-cull quads always render
public sealed class BakedModel
{
    //Grouped two-dimensionally by (layer, cullface); cullface=null means a no-cull quad
    private readonly Dictionary<(RenderLayer, Direction?), List<BakedQuad>> _quads = new();
    //Layers cache to avoid allocating on every query
    private HashSet<RenderLayer>? _layersCache;

    //GetQuads gets all quads of the given layer (including cullface and no-cull), backward compatible
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

    //GetCullfaceQuads gets cullface quads of all layers for the given direction, backward compatible
    //Chunk mesh generation should prefer GetCullfaceQuads(layer, dir) to avoid cross-layer confusion
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

    //GetCullfaceQuads gets cullface quads for the given layer + direction, for chunk mesh face-culling queries
    public IReadOnlyList<BakedQuad> GetCullfaceQuads(RenderLayer layer, Direction dir)
        => _quads.TryGetValue((layer, dir), out var list) ? list : Array.Empty<BakedQuad>();

    //GetNoCullQuads gets quads of the given layer without cullface; they always render and are never culled
    public IReadOnlyList<BakedQuad> GetNoCullQuads(RenderLayer layer)
        => _quads.TryGetValue((layer, null), out var list) ? list : Array.Empty<BakedQuad>();

    //AddQuad adds a quad into the given layer and cullface group
    //cullface=null goes to NoCull, is not culled and always renders
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

    //Layers returns all existing RenderLayers
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

//RenderLayer render layer, maps to vanilla RenderType
//Solid opaque blocks render first and write depth
//Cutout transparent-pixel blocks (glass) with alpha=0 or 1 use alpha test
//Translucent translucent blocks (water) render after sorting by distance
public enum RenderLayer
{
    Solid,
    Cutout,
    Translucent
}
