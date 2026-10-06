using System.Runtime.InteropServices;
using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.World;

//SectionMesh a compiled mesh holding vertices as byte[] and indices as int[] per RenderLayer
//Converted from ChunkMeshData's VertexConsumer3D.Vertices(List<float>) to byte[] via MemoryMarshal.AsBytes
//Indices generated as QUADS with 6/quad, matching ItemPipRenderer.GenerateQuadIndices
//stride 40 bytes position3+color1+uv2+light1+normal3, aligning with POSITION_COLOR_UV_LIGHT_NORMAL
//Vertices already bake the section offset (done by pose.Translate inside ChunkMeshBuilder.Build); SectionMesh does not handle the origin again
public sealed class SectionMesh
{
    private const int FloatsPerVertex = 10;
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;
    private const int LayerCount = 3;

    //_vertexData/_indexData indexed by (int)RenderLayer: Solid=0 Cutout=1 Translucent=2
    //null means the layer has no vertices; GetVertices returns an empty span
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

    //FromChunkMeshData converts each layer's vertices in ChunkMeshData to byte[], with indices generated as QUADS 6/quad
    //MemoryMarshal.AsBytes directly memcpys List<float>, matching ItemPipRenderer.VerticesToBytes
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

    //SetLayer copies List<float> vertices into byte[] and generates QUADS indices
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

    //GenerateQuadIndices 6 indices per quad 0-1-2-2-3-0, matching ItemPipRenderer
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
