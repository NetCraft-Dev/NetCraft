using System.Numerics;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//IWorldRenderer 世界渲染抽象接口
//LevelRenderer 在 NetCraft.Game 实现此接口避免 VulkanGuiApp 反向引用 NetCraft.Game 循环依赖
//VulkanGuiApp 持 IWorldRenderer 在 OnRecordCommandBuffer 调 Prepare/Upload/Draw
//ViewProj 属性供 VulkanGuiApp 上传 ViewProj UBO 到 shader
public interface IWorldRenderer
{
    //Prepare 构建 mesh 数据到内部 StagedVertexBuffer 每帧重建 W8 改异步缓存
    void Prepare();

    //Upload 上传顶点/索引到 GPU 跨帧复用 buffer
    void Upload(GpuDevice device);

    //ViewProj 当前帧 view*proj 矩阵 VulkanGuiApp 读此属性上传 set 0 UBO
    Matrix4x4 ViewProj { get; }

    //性能指标供 GameScreen F3 显示世界渲染统计
    int SectionCount { get; }
    int VisibleSectionCount { get; }
    int TotalVertexCount { get; }
    int DrawCallCount { get; }

    //Draw 按 Solid→Cutout→Translucent 顺序渲染 pipelineResolver 编译 pipeline descBinder 绑定 descriptor set
    void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Action<IRenderPass> descBinder);
}
