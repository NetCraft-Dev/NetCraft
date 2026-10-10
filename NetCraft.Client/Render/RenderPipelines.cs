using System.Collections.ObjectModel;
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

//RenderPipelines declarative pipeline registry, maps to vanilla RenderPipelines
//All pipelines are declared via PipelineBuilder snippet composition and registered into PIPELINES_BY_LOCATION
//The GUI series replaced the 4 hardcoded pipelines in VulkanGuiRenderer at stage 5
public static class RenderPipelines
{
    //s_pipelinesByLocation must be initialized before any static field that calls Register
    //C# static fields initialize in source declaration order, so placing it after GUI etc. would make it null
    private static readonly Dictionary<string, RenderPipeline> s_pipelinesByLocation = new();

    //GLOBALS_SNIPPET holds the global uniform block shared by all pipelines
    public static readonly Snippet GLOBALS_SNIPPET = PipelineBuilder.From()
        .WithBindGroupLayout(BindGroupLayouts.GLOBALS)
        .BuildSnippet();

    //MATRICES_SNIPPET holds the projection matrix uniform block, the base snippet of the GUI series
    public static readonly Snippet MATRICES_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .BuildSnippet();

    //GUI_SNIPPET GUI solid-color quad pipeline vertex shader core/gui + POSITION_COLOR + translucent blending
    public static readonly Snippet GUI_SNIPPET = PipelineBuilder.From(MATRICES_SNIPPET)
        .WithVertexShader("core/gui")
        .WithFragmentShader("core/gui")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //GUI_TEXTURED_SNIPPET GUI textured quad pipeline vertex shader core/position_tex_color + POSITION_TEX_COLOR + Sampler0
    public static readonly Snippet GUI_TEXTURED_SNIPPET = PipelineBuilder.From(MATRICES_SNIPPET)
        .WithVertexShader("core/position_tex_color")
        .WithFragmentShader("core/position_tex_color")
        .WithBindGroupLayout(BindGroupLayouts.SAMPLER0)
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_TEX_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //TEXT_SNIPPET text pipeline translucent blending + POSITION_TEX_COLOR + default depth test
    public static readonly Snippet TEXT_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .WithBindGroupLayout(BindGroupLayouts.SAMPLER0)
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithDepthStencilState(DepthStencilState.DEFAULT)
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_TEX_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //GUI_TEXT_SNIPPET GUI text snippet based on TEXT with the IS_GUI define and no depth test
    public static readonly Snippet GUI_TEXT_SNIPPET = PipelineBuilder.From(TEXT_SNIPPET)
        .WithShaderDefine("IS_GUI")
        .WithDepthStencilState((DepthStencilState?)null)
        .BuildSnippet();

    //GUI solid-color quad default GUI pipeline, for solid fills of widget borders/backgrounds
    public static readonly RenderPipeline GUI = Register(PipelineBuilder.From(GUI_SNIPPET)
        .WithLocation("pipeline/gui")
        .Build());

    //GUI_INVERT invert-blended GUI pipeline, for inverted elements like the crosshair
    public static readonly RenderPipeline GUI_INVERT = Register(PipelineBuilder.From(GUI_SNIPPET)
        .WithLocation("pipeline/gui_invert")
        .WithColorTargetState(new ColorTargetState(BlendFunction.INVERT))
        .Build());

    //GUI_TEXT_HIGHLIGHT text highlight additive-blend pipeline
    public static readonly RenderPipeline GUI_TEXT_HIGHLIGHT = Register(PipelineBuilder.From(GUI_SNIPPET)
        .WithLocation("pipeline/gui_text_highlight")
        .WithColorTargetState(new ColorTargetState(BlendFunction.ADDITIVE))
        .Build());

    //GUI_TEXTURED textured GUI pipeline, for buttons/icons/textured backgrounds
    public static readonly RenderPipeline GUI_TEXTURED = Register(PipelineBuilder.From(GUI_TEXTURED_SNIPPET)
        .WithLocation("pipeline/gui_textured")
        .Build());

    //GUI_TEXTURED_PREMULTIPLIED_ALPHA premultiplied-alpha textured GUI pipeline, for textures already premultiplied
    public static readonly RenderPipeline GUI_TEXTURED_PREMULTIPLIED_ALPHA = Register(PipelineBuilder.From(GUI_TEXTURED_SNIPPET)
        .WithLocation("pipeline/gui_textured_premultiplied_alpha")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT_PREMULTIPLIED_ALPHA))
        .Build());

    //GUI_TEXT GUI text pipeline, for font rendering
    public static readonly RenderPipeline GUI_TEXT = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .Build());

    //GUI_TEXT_GRAYSCALE GUI grayscale text pipeline, for grayscale text
    public static readonly RenderPipeline GUI_TEXT_GRAYSCALE = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_grayscale")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("IS_GRAYSCALE")
        .Build());

    //GUI_TEXT_SEE_THROUGH see-through text pipeline, maps to vanilla textSeeThrough
    //For text rendered through everything (e.g. nameplates); the shader reuses core/text, F7 uses the same shader and may add defines later
    public static readonly RenderPipeline GUI_TEXT_SEE_THROUGH = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_see_through")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("SEE_THROUGH")
        .Build());

    //GUI_TEXT_POLYGON_OFFSET polygon-offset text pipeline, maps to vanilla textPolygonOffset
    //For shadow rendering to avoid z-fighting; the shader reuses core/text, F7 uses the same shader and may add defines later
    public static readonly RenderPipeline GUI_TEXT_POLYGON_OFFSET = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_polygon_offset")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("POLYGON_OFFSET")
        .Build());

    //GUI_TEXT_GRAYSCALE_SEE_THROUGH grayscale see-through text pipeline, maps to vanilla textGrayscaleSeeThrough
    public static readonly RenderPipeline GUI_TEXT_GRAYSCALE_SEE_THROUGH = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_grayscale_see_through")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("IS_GRAYSCALE")
        .WithShaderDefine("SEE_THROUGH")
        .Build());

    //GUI_TEXT_GRAYSCALE_POLYGON_OFFSET grayscale polygon-offset text pipeline, maps to vanilla textGrayscalePolygonOffset
    public static readonly RenderPipeline GUI_TEXT_GRAYSCALE_POLYGON_OFFSET = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_grayscale_polygon_offset")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("IS_GRAYSCALE")
        .WithShaderDefine("POLYGON_OFFSET")
        .Build());

    //DEBUG_FILLED_SNIPPET debug fill snippet translucent blending + POSITION_COLOR + QUADS
    public static readonly Snippet DEBUG_FILLED_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .WithVertexShader("core/position_color")
        .WithFragmentShader("core/position_color")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .WithDepthStencilState(new DepthStencilState(CompareOp.GreaterOrEqual, false))
        .WithCull(false)
        .BuildSnippet();

    //DEBUG_QUADS debug quad pipeline
    public static readonly RenderPipeline DEBUG_QUADS = Register(PipelineBuilder.From(DEBUG_FILLED_SNIPPET)
        .WithLocation("pipeline/debug_quads")
        .WithCull(false)
        .Build());

    //POST_PROCESSING_SNIPPET post-processing snippet triangle list GLOBALS only
    public static readonly Snippet POST_PROCESSING_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithPrimitiveTopology(PrimitiveTopology.TriangleList)
        .BuildSnippet();

    //BLIT post-processing blit pipeline, for the screen quad blit
    public static readonly RenderPipeline BLIT = Register(PipelineBuilder.From(POST_PROCESSING_SNIPPET)
        .WithLocation("pipeline/blit")
        .WithVertexShader("core/screenquad")
        .WithFragmentShader("core/blit_screen")
        .WithBindGroupLayout(BindGroupLayouts.IN_SAMPLER)
        .Build());

    //BLUR Gaussian blur post-processing pipeline set 0 IN_SAMPLER set 1 BLUR_CONFIG, independent of GLOBALS
    //The BeforeBlur segment renders to offscreen then uses this pipeline for a horizontal+vertical 2-pass blur composited onto the swapchain
    public static readonly RenderPipeline BLUR = Register(PipelineBuilder.From()
        .WithLocation("pipeline/blur")
        .WithVertexShader("core/screenquad")
        .WithFragmentShader("core/blur")
        .WithBindGroupLayout(BindGroupLayouts.IN_SAMPLER)
        .WithBindGroupLayout(BindGroupLayouts.BLUR_CONFIG)
        .WithPrimitiveTopology(PrimitiveTopology.TriangleList)
        .Build());

    //ITEM_3D 3D item render pipeline set 0 MVP UBO set 1 Lighting UBO set 2 LightmapSampler set 3 AtlasSampler
    //ItemItemAtlas.DrawToSlot records commands to render the item into an AtlasTexture slot
    public static readonly RenderPipeline ITEM_3D = Register(PipelineBuilder.From()
        .WithLocation("pipeline/item_3d")
        .WithVertexShader("core/item_3d")
        .WithFragmentShader("core/item_3d")
        .WithBindGroupLayout(BindGroupLayouts.ITEM_MATRICES)
        .WithBindGroupLayout(BindGroupLayouts.ITEM_LIGHTING)
        .WithBindGroupLayout(BindGroupLayouts.ITEM_LIGHTMAP)
        .WithBindGroupLayout(BindGroupLayouts.ITEM_ATLAS)
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithDepthStencilState(DepthStencilState.DEFAULT)
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .Build());

    //Register adds a pipeline to the location-indexed table for external declarations like WorldRenderPipelines to reuse
    internal static RenderPipeline Register(RenderPipeline pipeline)
    {
        s_pipelinesByLocation[pipeline.Location] = pipeline;
        return pipeline;
    }

    //GetStaticPipelines returns all registered pipelines for PrecompilePipeline to warm the cache
    public static IReadOnlyCollection<RenderPipeline> GetStaticPipelines() => s_pipelinesByLocation.Values;

    //GetByLocation looks up a pipeline by location, returns null if absent
    public static RenderPipeline? GetByLocation(string location) =>
        s_pipelinesByLocation.TryGetValue(location, out var p) ? p : null;
}
