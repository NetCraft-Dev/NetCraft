using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//VulkanTriangleApp Vulkan 三角形 PoC 主程序
//继承 VulkanAppBase 只实现 pipeline 创建和命令录制其他由基类处理
public sealed unsafe class VulkanTriangleApp : VulkanAppBase
{
    private VulkanRenderPipeline _pipeline = null!;

    public VulkanTriangleApp() : base(800, 600) { }

    protected override string WindowTitle => "NetCraft.Gpu.Vulkan Triangle PoC";

    //OnCreatePipelineResources 创建三角形 pipeline 用内置 SpirvShaders shader
    protected override void OnCreatePipelineResources()
    {
        //PoC 用 SpirvShaders 内置 shader description 留 null 走 VulkanRenderPipeline 默认
        var description = new RenderPipelineDescription();
        _pipeline = new VulkanRenderPipeline(_device.Api, _device.Device, _swapchainImageFormat, _swapchainExtent, description);
    }

    //OnRecordCommandBuffer 4.3 改造传 colorImageView 走 dynamic rendering
    protected override void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView)
    {
        cmd.BeginRecording();
        cmd.BeginRenderPass(_pipeline, colorImageView);
        cmd.Draw(3);
        cmd.EndRenderPass();
        cmd.EndRecording();
    }

    //OnCleanupPipelineResources 销毁 pipeline 资源
    protected override void OnCleanupPipelineResources()
    {
        _pipeline.Dispose();
    }
}
