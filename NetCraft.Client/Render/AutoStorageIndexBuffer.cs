using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Render;

//AutoStorageIndexBuffer automatic index buffer, maps to vanilla AutoStorageIndexBuffer
//QUADS topology auto-generates 6 indices (0,1,2,2,3,0), avoiding a separate index buffer per Draw
//Other topologies generate no indices and draw non-indexed directly
//Staged on the CPU as List<uint>; the GpuBuffer is created and uploaded only during Upload, so unit tests do not need GpuDevice
public sealed class AutoStorageIndexBuffer : IDisposable
{
    //UInt32 indices are enough for GUI vertex counts; a frame never exceeds 2^32 vertices
    private readonly List<uint> _indices = new();
    private GpuBuffer? _indexBuffer;
    private bool _disposed;

    //Count total index count used to size the buffer before upload
    public int Count => _indices.Count;

    //IndexBuffer the GPU index buffer after upload; null before Upload
    public GpuBuffer? IndexBuffer => _indexBuffer;

    //Append generates indices for a Draw of the given topology and returns firstIndex and indexCount
    //QUADS generates 6 indices per 4 vertices (0,1,2,2,3,0); other topologies generate none and return 0
    //baseVertex is the Draw's starting vertex offset in the vertex buffer
    public (int firstIndex, int indexCount) Append(int baseVertex, int vertexCount, PrimitiveTopology topology)
    {
        if (topology != PrimitiveTopology.Quads)
            return (0, 0);
        var firstIndex = _indices.Count;
        var quads = vertexCount / 4;
        for (int i = 0; i < quads; i++)
        {
            var v = baseVertex + i * 4;
            //Two triangles v0-v1-v2 and v2-v3-v0 cover one quad
            _indices.Add((uint)(v + 0));
            _indices.Add((uint)(v + 1));
            _indices.Add((uint)(v + 2));
            _indices.Add((uint)(v + 2));
            _indices.Add((uint)(v + 3));
            _indices.Add((uint)(v + 0));
        }
        return (firstIndex, quads * 6);
    }

    //GetIndices returns an index snapshot for unit tests to verify order
    public ReadOnlySpan<uint> GetIndices() => _indices.ToArray();

    //Upload creates a UInt32 index buffer and uploads the index data
    //The buffer is reused across frames and rebuilt only when too small; host-visible, map+memcpy rewrites it each frame
    //Fixed the old bug where _indexBuffer!=null returned early, leaving the second frame's index data unupdated
    public void Upload(GpuDevice device)
    {
        if (_indices.Count == 0) return;
        var size = _indices.Count * sizeof(uint);
        if (_indexBuffer == null || _indexBuffer.Size < size)
        {
            _indexBuffer?.Dispose();
            _indexBuffer = device.CreateHostVisibleBuffer(size, GpuBufferUsage.IndexBuffer);
        }
        _indexBuffer.Upload<uint>(_indices.ToArray());
    }

    //EndFrame resets the index list while keeping the GPU buffer for cross-frame reuse
    //After the call Upload can upload again for the new frame
    public void EndFrame()
    {
        _indices.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _indexBuffer?.Dispose();
        _indexBuffer = null;
        _disposed = true;
    }
}
