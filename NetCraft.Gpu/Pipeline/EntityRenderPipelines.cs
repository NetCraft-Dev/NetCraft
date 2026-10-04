namespace NetCraft.Gpu.Pipeline;

//EntityRenderPipelines 实体渲染 pipeline 注册表对标原版 RenderTypes 的 entity 系列
//3 个 entity pipeline 共享 ENTITY_SNIPPET 仅 blend/define 差异
//set 0 MATRICES_PROJECTION 含 ViewProj mat4 CPU 端 PoseStack bake 实体世界变换进顶点 shader 端 Model=Identity
//set 1 SAMPLER0_SAMPLER1 atlas 纹理图集 + lightmap 光照贴图与 terrain 共用
//顶点格式 POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL 比 terrain 多 overlay 属性
public static class EntityRenderPipelines
{
    //ENTITY_SNIPPET entity 基础 snippet 所有 entity pipeline 共享
    //顶点格式 POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL Quads 拓扑转 TriangleList 深度 Less 写入
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

    //ENTITY_SOLID 不透明实体 pipeline 无 blend 无 alpha cutout
    //用于完全实体不透明模型
    public static readonly RenderPipeline ENTITY_SOLID = RenderPipelines.Register(PipelineBuilder.From(ENTITY_SNIPPET)
        .WithLocation("pipeline/entity_solid")
        .Build());

    //ENTITY_CUTOUT alpha cutout 实体 pipeline 无 blend shader discard alpha<0.5
    //用于镂空实体模型默认 pipeline
    public static readonly RenderPipeline ENTITY_CUTOUT = RenderPipelines.Register(PipelineBuilder.From(ENTITY_SNIPPET)
        .WithLocation("pipeline/entity_cutout")
        .WithShaderDefine("ALPHA_CUTOUT")
        .Build());

    //ENTITY_TRANSLUCENT 半透明实体 pipeline TRANSLUCENT blend 不写深度
    //用于半透明实体如史莱姆渲染在 Solid/Cutout 之后
    public static readonly RenderPipeline ENTITY_TRANSLUCENT = RenderPipelines.Register(PipelineBuilder.From(ENTITY_SNIPPET)
        .WithLocation("pipeline/entity_translucent")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .Build());
}
