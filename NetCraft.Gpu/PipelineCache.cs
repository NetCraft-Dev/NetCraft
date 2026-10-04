using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//PipelineCache pipeline 编译缓存对标原版 VulkanDevice.pipelineCache
//维护声明式 RenderPipeline → 编译产物 CompiledRenderPipeline 的映射
//同一 RenderPipeline 对象二次 Precompile 直接命中零编译开销
//保留 RenderPipelineDescription 重载兼容阶段 1 旧调用方
public sealed class PipelineCache : IDisposable
{
    private readonly GpuDevice _device;
    private readonly Dictionary<RenderPipeline, CompiledRenderPipeline> _declarations = new();
    private readonly Dictionary<RenderPipelineDescription, CompiledRenderPipeline> _descriptions = new();
    private bool _disposed;
    //HitCount/MissCount 缓存命中/未命中次数供性能验收验证运行时无 shader 编译
    public int HitCount { get; private set; }
    public int MissCount { get; private set; }

    public PipelineCache(GpuDevice device)
    {
        _device = device;
    }

    //Precompile 声明式 RenderPipeline 编译或返回缓存编译产物
    public CompiledRenderPipeline Precompile(RenderPipeline declaration)
    {
        if (_declarations.TryGetValue(declaration, out var cached))
        {
            HitCount++;
            return cached;
        }
        MissCount++;
        var compiled = _device.PrecompilePipeline(declaration);
        _declarations[declaration] = compiled;
        return compiled;
    }

    //Precompile 兼容旧 RenderPipelineDescription 调用方不缓存声明式映射
    public CompiledRenderPipeline Precompile(RenderPipelineDescription description)
    {
        if (_descriptions.TryGetValue(description, out var cached))
        {
            HitCount++;
            return cached;
        }
        MissCount++;
        var compiled = _device.CreateRenderPipeline(description);
        _descriptions[description] = compiled;
        return compiled;
    }

    //Clear 清空缓存并释放所有编译产物
    public void Clear()
    {
        foreach (var p in _declarations.Values) p.Dispose();
        foreach (var p in _descriptions.Values)
            if (!_declarations.ContainsValue(p)) p.Dispose();
        _declarations.Clear();
        _descriptions.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }
}
