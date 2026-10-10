using NetCraft.Client.Blaze3d.Systems;

namespace NetCraft.Client.Blaze3d.Pipeline;

//PipelineCache pipeline compile cache, maps to vanilla VulkanDevice.pipelineCache
//Maps declarative RenderPipeline → compiled CompiledRenderPipeline; the compiler callback is supplied by the backend
public sealed class PipelineCache : IDisposable
{
    private readonly Dictionary<RenderPipeline, CompiledRenderPipeline> _declarations = new();
    private bool _disposed;
    //HitCount/MissCount cache hit/miss counts for perf acceptance to verify no shader compilation at runtime
    public int HitCount { get; private set; }
    public int MissCount { get; private set; }

    //Precompile compiles a declarative RenderPipeline or returns the cached compiled artifact
    public CompiledRenderPipeline Precompile(RenderPipeline declaration, Func<RenderPipeline, CompiledRenderPipeline> compiler)
    {
        if (_declarations.TryGetValue(declaration, out var cached))
        {
            HitCount++;
            return cached;
        }
        MissCount++;
        var compiled = compiler(declaration);
        _declarations[declaration] = compiled;
        return compiled;
    }

    //Clear empties the cache and releases all compiled artifacts
    public void Clear()
    {
        foreach (var p in _declarations.Values) p.Dispose();
        _declarations.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }
}
