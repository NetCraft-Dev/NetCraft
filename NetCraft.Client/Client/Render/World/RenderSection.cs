using System.Threading;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.World;

//RenderSectionState section render state machine
//Empty not compiled, Queued in the compile queue, Compiling compiling in the background, Compiled done compiling awaiting upload, Uploaded uploaded and drawable
//Dirty uploaded but needs recompiling; the old buffer is still valid, Draw is skipped until recompiling finishes
public enum RenderSectionState : byte
{
    Empty,
    Queued,
    Compiling,
    Compiled,
    Uploaded,
    Dirty
}

//RenderSection single-section render data holder + state machine
//_mesh is published atomically with Interlocked.Exchange; the compile thread writes and the Render thread reads. Reference assignment is itself atomic and Exchange provides the publication barrier
//VertexBuffer/IndexBuffer are only read/written inside the Render thread's Lock, so no synchronization primitives are needed
//BoundingBox built from Pos<<4 for Frustum culling
public sealed class RenderSection
{
    public SectionPos Pos { get; }
    public AABB BoundingBox { get; }

    private volatile RenderSectionState _state;
    public RenderSectionState State => _state;

    private SectionMesh? _mesh;
    public SectionMesh? Mesh => _mesh;

    public GpuBuffer? VertexBuffer { get; private set; }
    public GpuBuffer? IndexBuffer { get; private set; }

    //Slices per-layer upload offset info, used by dispatcher.GetSectionSlice to build SectionSlice
    //null means the layer has no vertices; skipped at Draw
    public UploadedSlice?[] Slices { get; } = new UploadedSlice?[3];

    public bool HasUploadedBuffers => VertexBuffer is not null && IndexBuffer is not null;

    //IsRemoved chunk unload flag set to true by UnloadSection; compile/upload threads check and skip to prevent races
    private volatile bool _removed;
    public bool IsRemoved => _removed;

    public RenderSection(SectionPos pos)
    {
        Pos = pos;
        var minX = pos.X << 4;
        var minY = pos.Y << 4;
        var minZ = pos.Z << 4;
        BoundingBox = new AABB(minX, minY, minZ, minX + 16, minY + 16, minZ + 16);
    }

    //MarkRemoved marks as unloaded; the compile thread checks before PublishMesh to keep a removed section from entering the upload queue
    public void MarkRemoved() => _removed = true;

    //TryMarkDirty marks dirty and enqueues; CAS on state prevents duplicates
    //Empty/Compiled → Queued, no old buffer, enqueue directly
    //Uploaded → Dirty, the old buffer is still valid, Draw is skipped until recompiling finishes; UploadTerrainBuffers detects the old buffer and calls ReturnBuffer first
    //Queued/Compiling/Dirty return false; already queued or compiling, skip
    public bool TryMarkDirty()
    {
        while (true)
        {
            var prev = _state;
            if (prev == RenderSectionState.Queued
                || prev == RenderSectionState.Compiling
                || prev == RenderSectionState.Dirty)
                return false;
            var target = prev == RenderSectionState.Uploaded
                ? RenderSectionState.Dirty
                : RenderSectionState.Queued;
            if (Interlocked.CompareExchange(ref _state, target, prev) == prev)
                return true;
        }
    }

    //TryBeginCompile called by a worker after Take; Queued/Dirty → Compiling
    public bool TryBeginCompile()
    {
        while (true)
        {
            var prev = _state;
            if (prev != RenderSectionState.Queued && prev != RenderSectionState.Dirty)
                return false;
            if (Interlocked.CompareExchange(ref _state, RenderSectionState.Compiling, prev) == prev)
                return true;
        }
    }

    //PublishMesh called by the compile thread; atomically replaces mesh and transitions to Compiled for the Render thread to Upload
    public void PublishMesh(SectionMesh mesh)
    {
        Interlocked.Exchange(ref _mesh, mesh);
        Interlocked.Exchange(ref _state, RenderSectionState.Compiled);
    }

    //SetUploadedBuffers called inside the Render thread's Lock after upload; updates buffer references and transitions to Uploaded
    public void SetUploadedBuffers(GpuBuffer vb, GpuBuffer ib)
    {
        VertexBuffer = vb;
        IndexBuffer = ib;
        _state = RenderSectionState.Uploaded;
    }

    //ReleaseBuffers called inside the Render thread's Lock before recompiling or on unload; returns old buffers to the pool
    public void ReleaseBuffers(GpuBufferPool pool)
    {
        if (VertexBuffer is not null) pool.ReturnBuffer(VertexBuffer);
        if (IndexBuffer is not null) pool.ReturnBuffer(IndexBuffer);
        VertexBuffer = null;
        IndexBuffer = null;
        Array.Fill(Slices, null);
    }
}

//UploadedSlice per-layer upload offset info stored in RenderSection.Slices
//BaseVertex the starting vertex index of the layer's vertices in the vertex buffer; FirstIndex index start; IndexCount index count
public readonly record struct UploadedSlice(int BaseVertex, int FirstIndex, int IndexCount);
