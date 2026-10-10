using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
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

namespace NetCraft.Client.Render.World;

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
