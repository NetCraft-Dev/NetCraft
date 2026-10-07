using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//VulkanTriangleApp Vulkan triangle PoC main program
//Extends VulkanAppBase and only implements pipeline creation and command recording; the rest is handled by the base
public sealed unsafe class VulkanTriangleApp : VulkanAppBase
{
    private VulkanRenderPipeline _pipeline = null!;

    public VulkanTriangleApp() : base(800, 600) { }

    protected override string WindowTitle => "NetCraft.Gpu.Vulkan Triangle PoC";

    //OnCreatePipelineResources creates the triangle pipeline using the built-in SpirvShaders shader
    protected override void OnCreatePipelineResources()
    {
        //The PoC uses the built-in SpirvShaders shader and leaves description null, using VulkanRenderPipeline defaults
        var description = new RenderPipelineDescription();
        _pipeline = new VulkanRenderPipeline(_device.Api, _device.Device, _swapchainImageFormat, _swapchainExtent, description);
    }

    //OnRecordCommandBuffer 4.3 rework passes colorImageView for dynamic rendering
    protected override void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView)
    {
        cmd.BeginRecording();
        cmd.BeginRenderPass(_pipeline, colorImageView);
        cmd.Draw(3);
        cmd.EndRenderPass();
        cmd.EndRecording();
    }

    //OnCleanupPipelineResources destroys pipeline resources
    protected override void OnCleanupPipelineResources()
    {
        _pipeline.Dispose();
    }
}
