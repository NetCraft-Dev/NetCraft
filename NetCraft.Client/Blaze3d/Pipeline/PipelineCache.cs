using NetCraft.Client.Blaze3d.Pipeline;
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

namespace NetCraft.Client.Blaze3d.Pipeline;

//PipelineCache pipeline compile cache, maps to vanilla VulkanDevice.pipelineCache
//Maintains the mapping from declarative RenderPipeline → compiled CompiledRenderPipeline
//A second Precompile on the same RenderPipeline object hits directly with zero compile cost
//Keeps the RenderPipelineDescription overload for stage 1 legacy callers
public sealed class PipelineCache : IDisposable
{
    private readonly GpuDevice _device;
    private readonly Dictionary<RenderPipeline, CompiledRenderPipeline> _declarations = new();
    private readonly Dictionary<RenderPipelineDescription, CompiledRenderPipeline> _descriptions = new();
    private bool _disposed;
    //HitCount/MissCount cache hit/miss counts for perf acceptance to verify no shader compilation at runtime
    public int HitCount { get; private set; }
    public int MissCount { get; private set; }

    public PipelineCache(GpuDevice device)
    {
        _device = device;
    }

    //Precompile compiles a declarative RenderPipeline or returns the cached compiled artifact
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

    //Precompile legacy RenderPipelineDescription overload; callers do not cache the declarative mapping
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

    //Clear empties the cache and releases all compiled artifacts
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
