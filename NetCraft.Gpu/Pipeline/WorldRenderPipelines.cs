namespace NetCraft.Gpu.Pipeline;

//WorldRenderPipelines 世界渲染 pipeline 注册表对标原版 RenderPipelines 的 terrain 系列
//3 个 terrain pipeline 共享 TERRAIN_SNIPPET 仅 blend/define 差异
//set 0 MATRICES_PROJECTION 含 ViewProj mat4 CPU 端 bake section offset 进顶点 shader 端 Model=Identity
//set 1 SAMPLER0_SAMPLER1 atlas 纹理图集 + lightmap 光照贴图
//face shading bake 进顶点 color 不需要 Lighting UBO 方向光照
public static class WorldRenderPipelines
{
    //TERRAIN_SNIPPET terrain 基础 snippet 所有 terrain pipeline 共享
    //顶点格式 POSITION_COLOR_UV_LIGHT_NORMAL 与 ITEM_3D 一致 Quads 拓扑 深度 GEQUAL 写入
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

    //SOLID_TERRAIN 不透明 terrain pipeline 无 blend 无 alpha cutout
    //用于石头泥土等完全不透明方块 6 面全渲染深度写入
    public static readonly RenderPipeline SOLID_TERRAIN = RenderPipelines.Register(PipelineBuilder.From(TERRAIN_SNIPPET)
        .WithLocation("pipeline/solid_terrain")
        .Build());

    //CUTOUT_TERRAIN alpha cutout terrain pipeline 无 blend shader discard alpha<0.5
    //用于树叶花草等镂空纹理 不混合直接 discard 透明像素保持深度正确
    public static readonly RenderPipeline CUTOUT_TERRAIN = RenderPipelines.Register(PipelineBuilder.From(TERRAIN_SNIPPET)
        .WithLocation("pipeline/cutout_terrain")
        .WithShaderDefine("ALPHA_CUTOUT")
        .Build());

    //TRANSLUCENT_TERRAIN 半透明 terrain pipeline TRANSLUCENT blend 不写深度避免遮挡
    //用于水冰玻璃等半透明方块 按 RenderLayer 顺序在 Solid/Cutout 之后渲染
    public static readonly RenderPipeline TRANSLUCENT_TERRAIN = RenderPipelines.Register(PipelineBuilder.From(TERRAIN_SNIPPET)
        .WithLocation("pipeline/translucent_terrain")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .Build());
}
