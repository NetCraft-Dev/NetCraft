using System.Collections.ObjectModel;

namespace NetCraft.Gpu.Pipeline;

//RenderPipelines 声明式 pipeline 注册表对标原版 RenderPipelines
//所有 pipeline 通过 PipelineBuilder 声明 Snippet 组合 register 入 PIPELINES_BY_LOCATION 表
//GUI 系列用于 VulkanGuiRenderer 阶段 5 替换 4 个硬编码 pipeline
public static class RenderPipelines
{
    //s_pipelinesByLocation 必须先于所有调用 Register 的静态字段初始化
    //C# 静态字段按源码声明顺序初始化若放在 GUI 等 Register 调用之后则为 null
    private static readonly Dictionary<string, RenderPipeline> s_pipelinesByLocation = new();

    //GLOBALS_SNIPPET 含全局 uniform 块所有 pipeline 共享
    public static readonly Snippet GLOBALS_SNIPPET = PipelineBuilder.From()
        .WithBindGroupLayout(BindGroupLayouts.GLOBALS)
        .BuildSnippet();

    //MATRICES_SNIPPET 含投影矩阵 uniform 块 GUI 系列基础 snippet
    public static readonly Snippet MATRICES_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .BuildSnippet();

    //GUI_SNIPPET GUI 纯色 quad pipeline vertex shader core/gui + POSITION_COLOR + 半透明混合
    public static readonly Snippet GUI_SNIPPET = PipelineBuilder.From(MATRICES_SNIPPET)
        .WithVertexShader("core/gui")
        .WithFragmentShader("core/gui")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //GUI_TEXTURED_SNIPPET GUI 带纹理 quad pipeline vertex shader core/position_tex_color + POSITION_TEX_COLOR + Sampler0
    public static readonly Snippet GUI_TEXTURED_SNIPPET = PipelineBuilder.From(MATRICES_SNIPPET)
        .WithVertexShader("core/position_tex_color")
        .WithFragmentShader("core/position_tex_color")
        .WithBindGroupLayout(BindGroupLayouts.SAMPLER0)
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_TEX_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //TEXT_SNIPPET 文本 pipeline 半透明混合 + POSITION_TEX_COLOR + 默认深度测试
    public static readonly Snippet TEXT_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithBindGroupLayout(BindGroupLayouts.MATRICES_PROJECTION)
        .WithBindGroupLayout(BindGroupLayouts.SAMPLER0)
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT))
        .WithDepthStencilState(DepthStencilState.DEFAULT)
        .WithVertexBinding(0, DefaultVertexFormat.POSITION_TEX_COLOR)
        .WithPrimitiveTopology(PrimitiveTopology.Quads)
        .BuildSnippet();

    //GUI_TEXT_SNIPPET GUI 文本 snippet 基于 TEXT 加 IS_GUI define 去掉深度测试
    public static readonly Snippet GUI_TEXT_SNIPPET = PipelineBuilder.From(TEXT_SNIPPET)
        .WithShaderDefine("IS_GUI")
        .WithDepthStencilState((DepthStencilState?)null)
        .BuildSnippet();

    //GUI 纯色 quad 默认 GUI pipeline 用于 GUI 控件边框/背景纯色填充
    public static readonly RenderPipeline GUI = Register(PipelineBuilder.From(GUI_SNIPPET)
        .WithLocation("pipeline/gui")
        .Build());

    //GUI_INVERT 反色混合 GUI pipeline 用于十字准星等反相元素
    public static readonly RenderPipeline GUI_INVERT = Register(PipelineBuilder.From(GUI_SNIPPET)
        .WithLocation("pipeline/gui_invert")
        .WithColorTargetState(new ColorTargetState(BlendFunction.INVERT))
        .Build());

    //GUI_TEXT_HIGHLIGHT 文本高亮加色混合 pipeline
    public static readonly RenderPipeline GUI_TEXT_HIGHLIGHT = Register(PipelineBuilder.From(GUI_SNIPPET)
        .WithLocation("pipeline/gui_text_highlight")
        .WithColorTargetState(new ColorTargetState(BlendFunction.ADDITIVE))
        .Build());

    //GUI_TEXTURED 带纹理 GUI pipeline 用于按钮/图标/纹理化背景
    public static readonly RenderPipeline GUI_TEXTURED = Register(PipelineBuilder.From(GUI_TEXTURED_SNIPPET)
        .WithLocation("pipeline/gui_textured")
        .Build());

    //GUI_TEXTURED_PREMULTIPLIED_ALPHA 预乘 alpha 带纹理 GUI pipeline 用于已预乘 alpha 的纹理
    public static readonly RenderPipeline GUI_TEXTURED_PREMULTIPLIED_ALPHA = Register(PipelineBuilder.From(GUI_TEXTURED_SNIPPET)
        .WithLocation("pipeline/gui_textured_premultiplied_alpha")
        .WithColorTargetState(new ColorTargetState(BlendFunction.TRANSLUCENT_PREMULTIPLIED_ALPHA))
        .Build());

    //GUI_TEXT GUI 文本 pipeline 用于字体渲染
    public static readonly RenderPipeline GUI_TEXT = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .Build());

    //GUI_TEXT_GRAYSCALE GUI 文本灰度 pipeline 用于灰度文字
    public static readonly RenderPipeline GUI_TEXT_GRAYSCALE = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_grayscale")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("IS_GRAYSCALE")
        .Build());

    //GUI_TEXT_SEE_THROUGH 看背文本 pipeline 对标原版 textSeeThrough
    //用于穿透所有内容渲染的文字（如名牌）shader 复用 core/text F7 暂用相同 shader 后续按需加 define
    public static readonly RenderPipeline GUI_TEXT_SEE_THROUGH = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_see_through")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("SEE_THROUGH")
        .Build());

    //GUI_TEXT_POLYGON_OFFSET 多边形偏移文本 pipeline 对标原版 textPolygonOffset
    //用于阴影渲染避免 z-fighting shader 复用 core/text F7 暂用相同 shader 后续按需加 define
    public static readonly RenderPipeline GUI_TEXT_POLYGON_OFFSET = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_polygon_offset")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("POLYGON_OFFSET")
        .Build());

    //GUI_TEXT_GRAYSCALE_SEE_THROUGH 灰度看背文本 pipeline 对标原版 textGrayscaleSeeThrough
    public static readonly RenderPipeline GUI_TEXT_GRAYSCALE_SEE_THROUGH = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_grayscale_see_through")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("IS_GRAYSCALE")
        .WithShaderDefine("SEE_THROUGH")
        .Build());

    //GUI_TEXT_GRAYSCALE_POLYGON_OFFSET 灰度多边形偏移文本 pipeline 对标原版 textGrayscalePolygonOffset
    public static readonly RenderPipeline GUI_TEXT_GRAYSCALE_POLYGON_OFFSET = Register(PipelineBuilder.From(GUI_TEXT_SNIPPET)
        .WithLocation("pipeline/gui_text_grayscale_polygon_offset")
        .WithVertexShader("core/text")
        .WithFragmentShader("core/text")
        .WithShaderDefine("IS_GRAYSCALE")
        .WithShaderDefine("POLYGON_OFFSET")
        .Build());

    //DEBUG_FILLED_SNIPPET 调试填充 snippet 半透明混合 + POSITION_COLOR + QUADS
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

    //DEBUG_QUADS 调试四边形 pipeline
    public static readonly RenderPipeline DEBUG_QUADS = Register(PipelineBuilder.From(DEBUG_FILLED_SNIPPET)
        .WithLocation("pipeline/debug_quads")
        .WithCull(false)
        .Build());

    //POST_PROCESSING_SNIPPET 后处理 snippet 三角形列表 GLOBALS only
    public static readonly Snippet POST_PROCESSING_SNIPPET = PipelineBuilder.From(GLOBALS_SNIPPET)
        .WithPrimitiveTopology(PrimitiveTopology.TriangleList)
        .BuildSnippet();

    //BLIT 后处理 blit pipeline 用于屏幕四边形 blit
    public static readonly RenderPipeline BLIT = Register(PipelineBuilder.From(POST_PROCESSING_SNIPPET)
        .WithLocation("pipeline/blit")
        .WithVertexShader("core/screenquad")
        .WithFragmentShader("core/blit_screen")
        .WithBindGroupLayout(BindGroupLayouts.IN_SAMPLER)
        .Build());

    //BLUR 高斯模糊后处理 pipeline set 0 IN_SAMPLER set 1 BLUR_CONFIG 不依赖 GLOBALS
    //BeforeBlur 段渲染到 offscreen 后用此 pipeline 水平+垂直 2 pass 模糊再叠加到 swapchain
    public static readonly RenderPipeline BLUR = Register(PipelineBuilder.From()
        .WithLocation("pipeline/blur")
        .WithVertexShader("core/screenquad")
        .WithFragmentShader("core/blur")
        .WithBindGroupLayout(BindGroupLayouts.IN_SAMPLER)
        .WithBindGroupLayout(BindGroupLayouts.BLUR_CONFIG)
        .WithPrimitiveTopology(PrimitiveTopology.TriangleList)
        .Build());

    //ITEM_3D 3D 物品渲染 pipeline set 0 MVP UBO set 1 Lighting UBO set 2 LightmapSampler set 3 AtlasSampler
    //ItemItemAtlas.DrawToSlot 录制命令渲染物品到 AtlasTexture 槽位
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

    //Register 注册 pipeline 到按 location 索引表 供 WorldRenderPipelines 等外部声明复用
    internal static RenderPipeline Register(RenderPipeline pipeline)
    {
        s_pipelinesByLocation[pipeline.Location] = pipeline;
        return pipeline;
    }

    //GetStaticPipelines 返回所有已注册 pipeline 供 PrecompilePipeline 预热缓存
    public static IReadOnlyCollection<RenderPipeline> GetStaticPipelines() => s_pipelinesByLocation.Values;

    //GetByLocation 按 location 查找 pipeline 不存在返回 null
    public static RenderPipeline? GetByLocation(string location) =>
        s_pipelinesByLocation.TryGetValue(location, out var p) ? p : null;
}
