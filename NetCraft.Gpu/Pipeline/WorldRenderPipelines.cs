namespace NetCraft.Gpu.Pipeline;

//WorldRenderPipelines world render pipeline registry, maps to the terrain series of vanilla RenderPipelines
//The 3 terrain pipelines share TERRAIN_SNIPPET, differing only in blend/define
//set 0 MATRICES_PROJECTION with a ViewProj mat4; the CPU side bakes the section offset into the vertices and the shader uses Model=Identity
//set 1 SAMPLER0_SAMPLER1 atlas texture + lightmap
//Face shading is baked into the vertex color, so the Lighting UBO directional lighting is not needed
public static class WorldRenderPipelines
{
    //TERRAIN_SNIPPET terrain base snippet shared by all terrain pipelines
    //Vertex format POSITION_COLOR_UV_LIGHT_NORMAL, same as ITEM_3D, Quads topology, depth GEQUAL with write
    public static readonly Snippet TERRAIN_SNIPPET = PipelineBuilder.From()
        .WithVertexShader("core/terrain")
        .WithFragmentShader("core/terrain")
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .WithBindGroupLayout(BindGroupLayouts.SAMPLER0_SAMPLER1)
        .WithColorTargetState(ColorTargetState.DEFAULT)
        .WithDepthStencilState(DepthStencilState.DEFAULT)
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR_UV_LIGHT_NORMAL)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //SOLID_TERRAIN opaque terrain pipeline, no blend, no alpha cutout
    //Used for fully opaque blocks like stone and dirt; all 6 faces render with depth write
    public static readonly RenderPipeline SOLID_TERRAIN = RenderPipelines.Register(PipelineBuilder.From(TERRAIN_SNIPPET)
        .WithLocation("pipeline/solid_terrain")
        .Build());

    //CUTOUT_TERRAIN alpha-cutout terrain pipeline, no blend, shader discards alpha<0.5
    //Used for cutout textures like leaves and plants; no blend, transparent pixels discarded to keep depth correct
    public static readonly RenderPipeline CUTOUT_TERRAIN = RenderPipelines.Register(PipelineBuilder.From(TERRAIN_SNIPPET)
        .WithLocation("pipeline/cutout_terrain")
        .WithShaderDefine("ALPHA_CUTOUT")
        .Build());

    //TRANSLUCENT_TERRAIN translucent terrain pipeline TRANSLUCENT blend with no depth write to avoid occlusion
    //Used for translucent blocks like water, ice and glass; rendered after Solid/Cutout in RenderLayer order
    public static readonly RenderPipeline TRANSLUCENT_TERRAIN = RenderPipelines.Register(PipelineBuilder.From(TERRAIN_SNIPPET)
        .WithLocation("pipeline/translucent_terrain")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .Build());
}
