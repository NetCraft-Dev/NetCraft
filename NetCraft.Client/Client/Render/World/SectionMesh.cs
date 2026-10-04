using System.Runtime.InteropServices;
using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.World;

//SectionMesh 编译完成 mesh 按 RenderLayer 持顶点 byte[]+索引 int[]
//从 ChunkMeshData 的 VertexConsumer3D.Vertices(List<float>) 经 MemoryMarshal.AsBytes 转 byte[]
//索引按 QUADS 6/quad 生成与 ItemPipRenderer.GenerateQuadIndices 一致
//stride 40 字节 position3+color1+uv2+light1+normal3 对齐 POSITION_COLOR_UV_LIGHT_NORMAL
//顶点已 bake section offset ChunkMeshBuilder.Build 内 pose.Translate 完成 SectionMesh 不再处理 origin
public sealed class SectionMesh
{
    private const int FloatsPerVertex = 10;
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;
    private const int LayerCount = 3;

    //_vertexData/_indexData 按 (int)RenderLayer 索引 Solid=0 Cutout=1 Translucent=2
    //null 表示该 layer 无顶点 GetVertices 返回空 span
    private readonly byte[]?[] _vertexData = new byte[LayerCount][];
    private readonly int[]?[] _indexData = new int[LayerCount][];
    private readonly int[] _vertexCounts = new int[LayerCount];

    public int TotalVertexCount { get; private set; }
    public int TotalIndexCount { get; private set; }

    public bool HasLayer(RenderLayer layer) => _vertexData[(int)layer] is not null;

    public ReadOnlySpan<byte> GetVertices(RenderLayer layer)
        => _vertexData[(int)layer] ?? ReadOnlySpan<byte>.Empty;

    public ReadOnlySpan<int> GetIndices(RenderLayer layer)
        => _indexData[(int)layer] ?? ReadOnlySpan<int>.Empty;

    public int GetVertexCount(RenderLayer layer) => _vertexCounts[(int)layer];

    //FromChunkMeshData 把 ChunkMeshData 各 layer 顶点转 byte[] 索引按 QUADS 6/quad 生成
    //MemoryMarshal.AsBytes 直接 memcpy List<float> 与 ItemPipRenderer.VerticesToBytes 一致
    public static SectionMesh FromChunkMeshData(ChunkMeshData data)
    {
        var mesh = new SectionMesh();
        foreach (var layer in data.Layers)
        {
            var consumer = data.GetOrBeginLayer(layer);
            if (consumer.VertexCount == 0) continue;
            mesh.SetLayer(layer, consumer.Vertices);
        }
        return mesh;
    }

    //SetLayer 把 List<float> 顶点拷贝为 byte[] 生成 QUADS 索引
    private void SetLayer(RenderLayer layer, List<float> vertices)
    {
        var vertexCount = vertices.Count / FloatsPerVertex;
        var vertexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(vertices)).ToArray();
        _vertexData[(int)layer] = vertexBytes;
        _vertexCounts[(int)layer] = vertexCount;
        TotalVertexCount += vertexCount;
        var quadCount = vertexCount / VerticesPerQuad;
        var indices = GenerateQuadIndices(quadCount);
        _indexData[(int)layer] = indices;
        TotalIndexCount += indices.Length;
    }

    //GenerateQuadIndices 每 quad 6 索引 0-1-2-2-3-0 与 ItemPipRenderer 一致
    private static int[] GenerateQuadIndices(int quadCount)
    {
        var indices = new int[quadCount * IndicesPerQuad];
        for (int i = 0; i < quadCount; i++)
        {
            var baseVertex = i * VerticesPerQuad;
            var offset = i * IndicesPerQuad;
            indices[offset + 0] = baseVertex + 0;
            indices[offset + 1] = baseVertex + 1;
            indices[offset + 2] = baseVertex + 2;
            indices[offset + 3] = baseVertex + 2;
            indices[offset + 4] = baseVertex + 3;
            indices[offset + 5] = baseVertex + 0;
        }
        return indices;
    }
}
