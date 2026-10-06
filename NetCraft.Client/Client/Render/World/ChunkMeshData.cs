using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.World;

//ChunkMeshData chunk mesh output container, maps to vanilla SectionCompiler.Results.renderedLayers
//Groups VertexConsumer3D vertex data by RenderLayer
//ChunkMeshBuilder.Build produces this object for later StagedVertexBuffer upload or unit-test verification
//The first version stages with VertexConsumer3D's List<float>; StagedVertexBuffer integration is left to W5 (camera + pipeline)
public sealed class ChunkMeshData
{
    private readonly Dictionary<RenderLayer, VertexConsumer3D> _layers = new();

    //Layers list of written RenderLayers, for the upper layer to iterate and submit
    public IEnumerable<RenderLayer> Layers => _layers.Keys;

    //GetOrBeginLayer gets or creates the VertexConsumer3D for the given layer
    public VertexConsumer3D GetOrBeginLayer(RenderLayer layer)
    {
        if (!_layers.TryGetValue(layer, out var consumer))
        {
            consumer = new VertexConsumer3D();
            _layers[layer] = consumer;
        }
        return consumer;
    }

    //GetVertexCount returns the vertex count for the given layer; 0 when not written
    public int GetVertexCount(RenderLayer layer)
        => _layers.TryGetValue(layer, out var c) ? c.VertexCount : 0;

    //TotalVertexCount total vertex count across all layers
    public int TotalVertexCount
    {
        get
        {
            var sum = 0;
            foreach (var c in _layers.Values)
                sum += c.VertexCount;
            return sum;
        }
    }

    //HasLayer whether the given layer was written
    public bool HasLayer(RenderLayer layer) => _layers.ContainsKey(layer);

    //Clear clears all layers for object reuse
    public void Clear()
    {
        foreach (var c in _layers.Values)
            c.Clear();
        _layers.Clear();
    }
}
