using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Render;
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

//EntityRenderPipelines entity render pipeline registry, maps to the entity series of vanilla RenderTypes
//The 3 entity pipelines share ENTITY_SNIPPET, differing only in blend/define
//set 0 MATRICES_PROJECTION with a ViewProj mat4; the CPU-side PoseStack bakes the entity world transform into the vertices and the shader uses Model=Identity
//set 1 SAMPLER0_SAMPLER1 atlas texture + lightmap, shared with terrain
//Vertex format POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL, with an extra overlay attribute over terrain
public static class EntityRenderPipelines
{
    //ENTITY_SNIPPET entity base snippet shared by all entity pipelines
    //Vertex format POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL Quads topology converted to TriangleList depth Less with depth write
    public static readonly Snippet ENTITY_SNIPPET = PipelineBuilder.From()
        .WithVertexShader("core/entity")
        .WithFragmentShader("core/entity")
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .WithBindGroupLayout(BindGroupLayouts.SAMPLER0_SAMPLER1)
        .WithColorTargetState(ColorTargetState.DEFAULT)
        .WithDepthStencilState(DepthStencilState.DEFAULT)
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //ENTITY_SOLID opaque entity pipeline, no blend, no alpha cutout
    //Used for fully opaque solid models
    public static readonly RenderPipeline ENTITY_SOLID = RenderPipelines.Register(PipelineBuilder.From(ENTITY_SNIPPET)
        .WithLocation("pipeline/entity_solid")
        .Build());

    //ENTITY_CUTOUT alpha-cutout entity pipeline, no blend, shader discards alpha<0.5
    //Default pipeline for cutout entity models
    public static readonly RenderPipeline ENTITY_CUTOUT = RenderPipelines.Register(PipelineBuilder.From(ENTITY_SNIPPET)
        .WithLocation("pipeline/entity_cutout")
        .WithShaderDefine("ALPHA_CUTOUT")
        .Build());

    //ENTITY_TRANSLUCENT translucent entity pipeline TRANSLUCENT blend, no depth write
    //Used for translucent entities such as slimes, rendered after Solid/Cutout
    public static readonly RenderPipeline ENTITY_TRANSLUCENT = RenderPipelines.Register(PipelineBuilder.From(ENTITY_SNIPPET)
        .WithLocation("pipeline/entity_translucent")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .Build());
}
