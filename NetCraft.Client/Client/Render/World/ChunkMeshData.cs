using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.World;

//ChunkMeshData 区块 mesh 输出容器对标原版 SectionCompiler.Results.renderedLayers
//按 RenderLayer 分组持有 VertexConsumer3D 顶点数据
//ChunkMeshBuilder.Build 产出此对象供后续 StagedVertexBuffer 上传或单测验证
//首版用 VertexConsumer3D 的 List<float> 暂存 StagedVertexBuffer 集成留到 W5（相机+管线）
public sealed class ChunkMeshData
{
    private readonly Dictionary<RenderLayer, VertexConsumer3D> _layers = new();

    //Layers 已写入的 RenderLayer 列表供上层遍历提交
    public IEnumerable<RenderLayer> Layers => _layers.Keys;

    //GetOrBeginLayer 获取或创建指定 layer 的 VertexConsumer3D
    public VertexConsumer3D GetOrBeginLayer(RenderLayer layer)
    {
        if (!_layers.TryGetValue(layer, out var consumer))
        {
            consumer = new VertexConsumer3D();
            _layers[layer] = consumer;
        }
        return consumer;
    }

    //GetVertexCount 返回指定 layer 的顶点数未写入返回 0
    public int GetVertexCount(RenderLayer layer)
        => _layers.TryGetValue(layer, out var c) ? c.VertexCount : 0;

    //TotalVertexCount 所有 layer 顶点总数
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

    //HasLayer 是否写入了指定 layer
    public bool HasLayer(RenderLayer layer) => _layers.ContainsKey(layer);

    //Clear 清空所有 layer 供对象复用
    public void Clear()
    {
        foreach (var c in _layers.Values)
            c.Clear();
        _layers.Clear();
    }
}
