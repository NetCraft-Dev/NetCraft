using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//GuiElementRenderState GUI 元素渲染状态接口对标原版 GuiElementRenderState
//RenderState 子类实现此接口提交到 GuiRenderState 参与 SortElements 排序合批
//不可变值对象携带 pipeline/texture/pose 快照/scissor 快照
public interface GuiElementRenderState
{
    //BuildVertices 构造顶点到 consumer
    void BuildVertices(IVertexConsumer consumer);
    //Pipeline 声明式渲染管线由 GuiRenderer 通过 PipelineCache 编译为 CompiledRenderPipeline
    RenderPipeline Pipeline { get; }
    //TextureSetup 纹理绑定配置
    TextureSetup TextureSetup { get; }
    //ScissorArea 裁剪矩形
    ScreenRectangle ScissorArea { get; }
    //Bounds 用于层级相交判断由元素几何+pose+scissor 计算
    ScreenRectangle Bounds { get; }
}
