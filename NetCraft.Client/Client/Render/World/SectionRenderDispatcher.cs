using System.Collections.Concurrent;
using System.Threading;
using NetCraft.Client.Level;
using NetCraft.Client.Render.Culling;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Primitives;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Render.World;

//SectionRenderDispatcher async dispatcher, maps to vanilla SectionRenderDispatcher
//Background threads compile chunk meshes + mesh cache + dirty marking; the Render thread only does Upload+Draw
//_sections holds all RenderSections; _compileQueue is consumed by background threads, _uploadQueue by the Render thread
//_mesh is published atomically with Interlocked.Exchange; the compile thread writes and the Render thread reads
//_syncRoot is a coarse lock protecting only the Render thread's combined Upload+Draw; compile threads do not take the lock
//GpuBufferPool reuses buffers across frames; ReturnBuffer is called on recompile/unload without EndFrame
public sealed class SectionRenderDispatcher : IDisposable
{
    private readonly ClientLevel _level;
    private readonly ChunkMeshBuilder _meshBuilder;
    private readonly GpuBufferPool _bufferPool;
    private readonly ConcurrentDictionary<long, RenderSection> _sections = new();
    //_compileQueue background threads block on BlockingCollection.GetConsumingEnumerable
    private readonly BlockingCollection<RenderSection> _compileQueue = new(new ConcurrentQueue<RenderSection>());
    //_uploadQueue the compile thread Enqueues, the Render thread TryDequeues inside the Lock
    private readonly ConcurrentQueue<RenderSection> _uploadQueue = new();
    private readonly Thread[] _workers;
    private readonly object _syncRoot = new();
    private readonly ViewArea _viewArea = new();
    private const int VertexStride = 40;
    private volatile bool _running;
    private bool _disposed;

    public int SectionCount => _sections.Count;
    public int PendingUploadCount => _uploadQueue.Count;
    public int VisibleSectionCount => _viewArea.VisibleCount;
    //BufferPoolInUseCount currently borrowed GPU buffer count, for tests to verify no leaks; should == UploadedSectionCount * 2
    public int BufferPoolInUseCount => _bufferPool.InUseCount;
    //UploadedSectionCount number of uploaded sections; iterates _sections counting Uploaded state, for tests to verify buffer counts
    public int UploadedSectionCount
    {
        get
        {
            var count = 0;
            foreach (var section in _sections.Values)
                if (section.State == RenderSectionState.Uploaded) count++;
            return count;
        }
    }

    //workerCount defaults to max(1, ProcessorCount-1); CPU-bound work uses dedicated threads, not the ThreadPool
    public SectionRenderDispatcher(ClientLevel level, ChunkMeshBuilder meshBuilder, GpuBufferPool bufferPool, int? workerCount = null)
    {
        _level = level;
        _meshBuilder = meshBuilder;
        _bufferPool = bufferPool;
        var count = workerCount ?? Math.Max(1, Environment.ProcessorCount - 1);
        _workers = new Thread[count];
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        for (var i = 0; i < _workers.Length; i++)
        {
            _workers[i] = new Thread(WorkerLoop) { IsBackground = true, Name = $"SectionRenderDispatcher-Worker-{i}" };
            _workers[i].Start();
        }
    }

    //Stop calls CompleteAdding so workers exit GetConsumingEnumerable, then Join waits for the threads to end
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _compileQueue.CompleteAdding();
        foreach (var worker in _workers)
            worker?.Join();
    }

    //SetCameraPosition called on the Render thread; ViewArea.Update diffs the frustum and calls MarkDirty for newly visible sections to compile
    //The camera parameter is unused in the first version, reserved for a future position-range traversal optimization; currently it iterates all loaded chunks
    public void SetCameraPosition(Camera camera, Frustum frustum)
    {
        _viewArea.Update(frustum, _level, pos => MarkDirty(pos));
    }

    //MarkDirty marks dirty and enqueues the section + 6 neighbors; cross-section boundary face culling must be recomputed
    //Unloaded neighbor sections are skipped without creating a RenderSection, to avoid _sections bloat
    public void MarkDirty(SectionPos pos)
    {
        MarkDirtySingle(pos);
        MarkDirtySingle(new SectionPos(pos.X + 1, pos.Y, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X - 1, pos.Y, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y + 1, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y - 1, pos.Z));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y, pos.Z + 1));
        MarkDirtySingle(new SectionPos(pos.X, pos.Y, pos.Z - 1));
    }

    private void MarkDirtySingle(SectionPos pos)
    {
        if (_level.GetSection(pos.X, pos.Y, pos.Z) is null) return;
        var section = _sections.GetOrAdd(pos.AsLong(), _ => new RenderSection(pos));
        if (section.TryMarkDirty())
        {
            try { _compileQueue.Add(section); }
            catch (InvalidOperationException) { } //Add throws after CompleteAdding; ignore
        }
    }

    //Lock/Unlock the Render thread holds the lock during Upload+Draw; compile threads do not take the lock and only publish mesh atomically
    public void Lock() => Monitor.Enter(_syncRoot);
    public void Unlock() => Monitor.Exit(_syncRoot);

    //UploadTerrainBuffers called inside the Render thread's Lock; dequeues everything from _uploadQueue, borrows buffers, and uploads
    //On recompile, return old buffers first then borrow new; one vb+ib per section covering all layers
    //Layer vertices are concatenated and indices offset by baseVertex; UploadedSlice is recorded for GetSectionSlice
    public void UploadTerrainBuffers()
    {
        while (_uploadQueue.TryDequeue(out var section))
        {
            //Skip unloaded sections without borrowing buffers; UnloadSection already returned the old buffers
            if (section.IsRemoved) continue;
            var mesh = section.Mesh;
            if (mesh is null || mesh.TotalVertexCount == 0)
            {
                //Empty mesh: release old buffers and mark Uploaded, but Slices are all null so Draw skips
                if (section.HasUploadedBuffers) section.ReleaseBuffers(_bufferPool);
                section.SetUploadedBuffers(_bufferPool.GetBuffer(4, GpuBufferUsage.VertexBuffer), _bufferPool.GetBuffer(4, GpuBufferUsage.IndexBuffer));
                continue;
            }
            if (section.HasUploadedBuffers) section.ReleaseBuffers(_bufferPool);
            var vertexBytes = new byte[mesh.TotalVertexCount * VertexStride];
            var indexData = new int[mesh.TotalIndexCount];
            var vertexByteOffset = 0;
            var indexOffset = 0;
            var baseVertex = 0;
            var firstIndex = 0;
            foreach (var layer in s_layers)
            {
                if (!mesh.HasLayer(layer)) continue;
                var vertices = mesh.GetVertices(layer);
                var vertexCount = mesh.GetVertexCount(layer);
                var indices = mesh.GetIndices(layer);
                vertices.CopyTo(new Span<byte>(vertexBytes, vertexByteOffset, vertices.Length));
                for (var i = 0; i < indices.Length; i++)
                    indexData[indexOffset + i] = indices[i] + baseVertex;
                section.Slices[(int)layer] = new UploadedSlice(baseVertex, firstIndex, indices.Length);
                vertexByteOffset += vertices.Length;
                indexOffset += indices.Length;
                baseVertex += vertexCount;
                firstIndex += indices.Length;
            }
            var vb = _bufferPool.GetBuffer(vertexBytes.Length, GpuBufferUsage.VertexBuffer);
            var ib = _bufferPool.GetBuffer(indexData.Length * sizeof(int), GpuBufferUsage.IndexBuffer);
            vb.Upload<byte>(vertexBytes);
            ib.Upload<int>(indexData);
            section.SetUploadedBuffers(vb, ib);
        }
    }

    //GetSectionSlice called inside the Render thread's Lock; returns the section's UploadedSlice + buffer references
    //Returns null when State!=Uploaded or the layer has no vertices; Draw skips
    public SectionSlice? GetSectionSlice(SectionPos pos, RenderLayer layer)
    {
        if (!_sections.TryGetValue(pos.AsLong(), out var section)) return null;
        if (section.State != RenderSectionState.Uploaded) return null;
        var slice = section.Slices[(int)layer];
        if (slice is null) return null;
        return new SectionSlice(section.VertexBuffer!, section.IndexBuffer!, slice.Value.BaseVertex, slice.Value.FirstIndex, slice.Value.IndexCount);
    }

    //EnumerateVisibleSections called inside the Render thread's Lock; iterates uploaded sections and tests the frustum
    //Also filters by _viewArea.IsVisible to avoid rendering sections of unloaded chunks; after a chunk unloads, ViewArea refreshes next frame so it is no longer visible
    //ConcurrentDictionary.Values returns a snapshot so traversal is safe; the yield is consumed immediately by the foreach inside the Lock
    public IEnumerable<RenderSection> EnumerateVisibleSections(Frustum frustum)
    {
        foreach (var section in _sections.Values)
        {
            if (section.State != RenderSectionState.Uploaded) continue;
            if (!_viewArea.IsVisible(section.Pos)) continue;
            if (frustum.IsVisible(section.BoundingBox)) yield return section;
        }
    }

    //UnloadSection called on chunk unload inside the Render thread's Lock; returns buffers and removes the section
    //MarkRemoved prevents races: the compile thread checks before PublishMesh and skips; the upload thread checks after dequeue and skips without borrowing
    public void UnloadSection(SectionPos pos)
    {
        if (_sections.TryRemove(pos.AsLong(), out var section))
        {
            section.MarkRemoved();
            section.ReleaseBuffers(_bufferPool);
        }
    }

    //WorkerLoop background thread main loop; blocks on GetConsumingEnumerable, and after compiling enqueues into _uploadQueue
    //Holds ClientLevel's read lock while calling GetSection+Snapshot+Build, preventing SetBlockState from modifying the section's internal PalettedContainer
    //PublishMesh+Enqueue happen outside the read lock to reduce lock hold time; if the chunk is unloaded and rawSection is null, skip and leave it in Compiling
    private void WorkerLoop()
    {
        foreach (var section in _compileQueue.GetConsumingEnumerable())
        {
            if (!_running) break;
            if (!section.TryBeginCompile()) continue;
            SectionMesh? mesh = null;
            var compileSuccess = false;
            try
            {
                _level.EnterReadLock();
                try
                {
                    var rawSection = _level.GetSection(section.Pos.X, section.Pos.Y, section.Pos.Z);
                    if (rawSection is null)
                    {
                        //Chunk already unloaded; the section is stuck in Compiling until UnloadSection cleans it up, not handled in the first version
                        compileSuccess = false;
                    }
                    else if (rawSection.HasOnlyAir())
                    {
                        mesh = new SectionMesh();
                        compileSuccess = true;
                    }
                    else
                    {
                        var regionCache = RenderRegionCache.Snapshot(_level, section.Pos);
                        var originX = section.Pos.X << 4;
                        var originY = section.Pos.Y << 4;
                        var originZ = section.Pos.Z << 4;
                        var meshData = _meshBuilder.Build(rawSection, regionCache, originX, originY, originZ);
                        mesh = SectionMesh.FromChunkMeshData(meshData);
                        compileSuccess = true;
                    }
                }
                finally { _level.ExitReadLock(); }
                //The section was unloaded during compilation; skip publishing, UnloadSection already cleaned the buffers
                if (compileSuccess && mesh is not null && !section.IsRemoved)
                {
                    section.PublishMesh(mesh);
                    _uploadQueue.Enqueue(section);
                }
            }
            catch
            {
                if (_running && section.TryMarkDirty())
                {
                    try { _compileQueue.Add(section); } catch (InvalidOperationException) { }
                }
            }
        }
    }

    private static readonly RenderLayer[] s_layers = { RenderLayer.Solid, RenderLayer.Cutout, RenderLayer.Translucent };

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _compileQueue.Dispose();
        foreach (var section in _sections.Values)
        {
            if (section.HasUploadedBuffers) section.ReleaseBuffers(_bufferPool);
        }
        _sections.Clear();
        _disposed = true;
    }
}

//SectionSlice the render slice returned by dispatcher.GetSectionSlice for LevelRenderer.Draw to submit
//VertexBuffer/IndexBuffer shared, one buffer per section; BaseVertex/FirstIndex locate the layer offset
public readonly record struct SectionSlice(GpuBuffer VertexBuffer, GpuBuffer IndexBuffer, int BaseVertex, int FirstIndex, int IndexCount);
